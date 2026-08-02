using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using GAS.Core;
using GAS.Core.GameplayEffect;
using GAS.StateSystem;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GAS.Core.Effect.Test
{
    /// <summary>
    /// GE 压测 输出可对比报告 挂场景物体后 Play 或右键 Run
    /// </summary>
    public class EffectPerfBenchmark : MonoBehaviour
    {
        /// <summary> 报告标签 优化前后请改 baseline / after-opt </summary>
        [SerializeField] private string reportTag = "baseline";

        /// <summary> 是否 Start 自动跑 </summary>
        [SerializeField] private bool runOnStart = true;

        /// <summary> 目标数量 模拟多单位 </summary>
        [SerializeField] private int targetCount = 50;

        /// <summary> 每目标独立层上限 </summary>
        [SerializeField] private int independentStackLimit = 5;

        /// <summary> 共享层上限 </summary>
        [SerializeField] private int sharedStackLimit = 10;

        /// <summary> Update 帧数 </summary>
        [SerializeField] private int tickFrames = 600;

        /// <summary> 每帧 dt </summary>
        [SerializeField] private float tickDelta = 0.016f;

        /// <summary> 每目标挂几种不同独立 Dot </summary>
        [SerializeField] private int independentEffectKinds = 3;

        /// <summary> 预热轮数 不计入报告 </summary>
        [SerializeField] private int warmupRounds = 1;

        private void Start()
        {
            if (runOnStart)
                RunBenchmark();
        }

        /// <summary>
        /// 跑压测并打报告
        /// </summary>
        [ContextMenu("Run Benchmark")]
        public void RunBenchmark()
        {
            bool oldQuiet = GEManager.QuietMode;
            GEManager.QuietMode = true;

            StringBuilder report = new StringBuilder(2048);
            report.AppendLine("=== GE Perf Report ===");
            report.AppendLine($"tag={reportTag}");
            report.AppendLine($"unity={Application.unityVersion} platform={Application.platform}");
            report.AppendLine(
                $"config targets={targetCount} indStacks={independentStackLimit} " +
                $"sharedStacks={sharedStackLimit} kinds={independentEffectKinds} " +
                $"ticks={tickFrames} dt={tickDelta} warmup={warmupRounds}");
            report.AppendLine();

            try
            {
                for (int i = 0; i < warmupRounds; i++)
                {
                    RunCaseA_SharedUpdate(null);
                    RunCaseB_IndependentUpdate(null);
                    RunCaseC_SharedReapply(null);
                    RunCaseD_MultiKindIndependentUpdate(null);
                }

                RunCaseA_SharedUpdate(report);
                RunCaseB_IndependentUpdate(report);
                RunCaseC_SharedReapply(report);
                RunCaseD_MultiKindIndependentUpdate(report);

                report.AppendLine("=== End Report ===");
                report.AppendLine("请整段复制发给我 优化后再用同一 config 跑一遍对比");
                Debug.Log(report.ToString());
            }
            finally
            {
                GEManager.QuietMode = oldQuiet;
            }
        }

        #region Cases

        /// <summary>
        /// A 每目标1个共享Dot 叠满后长时间 Update
        /// </summary>
        private void RunCaseA_SharedUpdate(StringBuilder report)
        {
            CaseResult result = Measure("A_SharedFill+Update", () =>
            {
                List<Target> targetList = CreateTargets(targetCount);
                GameplayEffectData dot = CreateDot(
                    "PerfSharedDot",
                    aggregation: E_EffectStackAggregation.SharedCount,
                    stackLimit: sharedStackLimit,
                    stacksPerApplication: 1,
                    duration: 9999f,
                    period: 0.2f,
                    executeOnApply: false);

                long applyMs = TimeApply(() =>
                {
                    for (int t = 0; t < targetList.Count; t++)
                    {
                        Target target = targetList[t];
                        for (int s = 0; s < sharedStackLimit; s++)
                            target.GE.ApplyGE(dot, target.Root);
                    }
                });

                int applied = SumApplied(targetList);
                long updateMs = TimeUpdate(targetList, tickFrames, tickDelta);
                DestroyTargets(targetList);
                DestroyAsset(dot);
                return new CaseMetrics(applyMs, updateMs, applied, targetCount);
            });

            AppendCase(report, result);
        }

        /// <summary>
        /// B 每目标1种独立Dot 叠满后 Update
        /// </summary>
        private void RunCaseB_IndependentUpdate(StringBuilder report)
        {
            CaseResult result = Measure("B_IndependentFill+Update", () =>
            {
                List<Target> targetList = CreateTargets(targetCount);
                GameplayEffectData dot = CreateDot(
                    "PerfIndDot",
                    aggregation: E_EffectStackAggregation.IndependentInstances,
                    stackLimit: independentStackLimit,
                    stacksPerApplication: 1,
                    duration: 9999f,
                    period: 0.2f,
                    executeOnApply: false);

                long applyMs = TimeApply(() =>
                {
                    for (int t = 0; t < targetList.Count; t++)
                    {
                        Target target = targetList[t];
                        for (int s = 0; s < independentStackLimit; s++)
                            target.GE.ApplyGE(dot, target.Root);
                    }
                });

                int applied = SumApplied(targetList);
                long updateMs = TimeUpdate(targetList, tickFrames, tickDelta);
                DestroyTargets(targetList);
                DestroyAsset(dot);
                return new CaseMetrics(applyMs, updateMs, applied, targetCount);
            });

            AppendCase(report, result);
        }

        /// <summary>
        /// C 共享反复再应用 侧重 HandleStacking
        /// </summary>
        private void RunCaseC_SharedReapply(StringBuilder report)
        {
            CaseResult result = Measure("C_SharedReapplySpam", () =>
            {
                List<Target> targetList = CreateTargets(targetCount);
                GameplayEffectData dot = CreateDot(
                    "PerfReapplyDot",
                    aggregation: E_EffectStackAggregation.SharedCount,
                    stackLimit: sharedStackLimit,
                    stacksPerApplication: 1,
                    duration: 9999f,
                    period: 1f,
                    executeOnApply: false);

                int reapplyTimes = sharedStackLimit * 20;
                long applyMs = TimeApply(() =>
                {
                    for (int t = 0; t < targetList.Count; t++)
                    {
                        Target target = targetList[t];
                        for (int s = 0; s < reapplyTimes; s++)
                            target.GE.ApplyGE(dot, target.Root);
                    }
                });

                int applied = SumApplied(targetList);
                long updateMs = TimeUpdate(targetList, tickFrames / 2, tickDelta);
                DestroyTargets(targetList);
                DestroyAsset(dot);
                return new CaseMetrics(applyMs, updateMs, applied, reapplyTimes);
            });

            AppendCase(report, result);
        }

        /// <summary>
        /// D 每目标多种独立Dot 叠满 Update 更接近实战密度
        /// </summary>
        private void RunCaseD_MultiKindIndependentUpdate(StringBuilder report)
        {
            CaseResult result = Measure("D_MultiKindIndependent+Update", () =>
            {
                List<Target> targetList = CreateTargets(targetCount);
                List<GameplayEffectData> dotList = new List<GameplayEffectData>(independentEffectKinds);
                for (int k = 0; k < independentEffectKinds; k++)
                {
                    dotList.Add(CreateDot(
                        "PerfKind" + k,
                        aggregation: E_EffectStackAggregation.IndependentInstances,
                        stackLimit: independentStackLimit,
                        stacksPerApplication: 1,
                        duration: 9999f,
                        period: 0.25f,
                        executeOnApply: false));
                }

                long applyMs = TimeApply(() =>
                {
                    for (int t = 0; t < targetList.Count; t++)
                    {
                        Target target = targetList[t];
                        for (int k = 0; k < dotList.Count; k++)
                        {
                            for (int s = 0; s < independentStackLimit; s++)
                                target.GE.ApplyGE(dotList[k], target.Root);
                        }
                    }
                });

                int applied = SumApplied(targetList);
                long updateMs = TimeUpdate(targetList, tickFrames, tickDelta);
                DestroyTargets(targetList);
                for (int i = 0; i < dotList.Count; i++)
                    DestroyAsset(dotList[i]);
                return new CaseMetrics(applyMs, updateMs, applied, independentEffectKinds);
            });

            AppendCase(report, result);
        }

        #endregion

        #region Measure

        /// <summary>
        /// 单用例测量
        /// </summary>
        private CaseResult Measure(string name, Func<CaseMetrics> body)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            long memBefore = GC.GetTotalMemory(false);

            Stopwatch totalWatch = Stopwatch.StartNew();
            CaseMetrics metrics = body();
            totalWatch.Stop();

            long memAfter = GC.GetTotalMemory(false);
            int gen0After = GC.CollectionCount(0);
            int gen1After = GC.CollectionCount(1);
            int gen2After = GC.CollectionCount(2);

            return new CaseResult
            {
                Name = name,
                ApplyMs = metrics.ApplyMs,
                UpdateMs = metrics.UpdateMs,
                TotalMs = totalWatch.ElapsedMilliseconds,
                AppliedCount = metrics.AppliedCount,
                Extra = metrics.Extra,
                MemDeltaBytes = memAfter - memBefore,
                Gen0 = gen0After - gen0Before,
                Gen1 = gen1After - gen1Before,
                Gen2 = gen2After - gen2Before
            };
        }

        /// <summary>
        /// 计时 Apply 段
        /// </summary>
        private static long TimeApply(Action action)
        {
            Stopwatch watch = Stopwatch.StartNew();
            action();
            watch.Stop();
            return watch.ElapsedMilliseconds;
        }

        /// <summary>
        /// 计时 Update 段
        /// </summary>
        private static long TimeUpdate(List<Target> targetList, int frames, float dt)
        {
            Stopwatch watch = Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
            {
                for (int t = 0; t < targetList.Count; t++)
                    targetList[t].GE.UpdateGE(dt);
            }
            watch.Stop();
            return watch.ElapsedMilliseconds;
        }

        /// <summary>
        /// 写入报告行
        /// </summary>
        private static void AppendCase(StringBuilder report, CaseResult result)
        {
            if (report is null) return;

            report.AppendLine($"[{result.Name}]");
            report.AppendLine(
                $"  applyMs={result.ApplyMs} updateMs={result.UpdateMs} totalMs={result.TotalMs}");
            report.AppendLine(
                $"  appliedCount={result.AppliedCount} extra={result.Extra}");
            report.AppendLine(
                $"  memDeltaBytes={result.MemDeltaBytes} gen0={result.Gen0} gen1={result.Gen1} gen2={result.Gen2}");
            report.AppendLine();
        }

        #endregion

        #region Fixture helpers

        /// <summary>
        /// 创建多目标
        /// </summary>
        private static List<Target> CreateTargets(int count)
        {
            List<Target> list = new List<Target>(count);
            for (int i = 0; i < count; i++)
                list.Add(Target.Create("PerfTarget_" + i));
            return list;
        }

        /// <summary>
        /// 销毁多目标
        /// </summary>
        private static void DestroyTargets(List<Target> targetList)
        {
            for (int i = 0; i < targetList.Count; i++)
                targetList[i].Dispose();
        }

        /// <summary>
        /// 已应用总数
        /// </summary>
        private static int SumApplied(List<Target> targetList)
        {
            int sum = 0;
            for (int i = 0; i < targetList.Count; i++)
                sum += targetList[i].GE.AppliedCount;
            return sum;
        }

        /// <summary>
        /// 创建周期 Dot 配置
        /// </summary>
        private static GameplayEffectData CreateDot(
            string name,
            E_EffectStackAggregation aggregation,
            int stackLimit,
            int stacksPerApplication,
            float duration,
            float period,
            bool executeOnApply)
        {
            GameplayEffectData data = ScriptableObject.CreateInstance<GameplayEffectData>();
            data.name = name;
            SetField(data, "durationPolicy", E_EffectDuration.HasDuration);
            SetField(data, "duration", duration);
            SetField(data, "isPeriodic", true);
            SetField(data, "period", period);
            SetField(data, "executePeriodicEffectOnApplication", executeOnApply);
            SetField(data, "stackAggregation", aggregation);
            SetField(data, "stackLimit", stackLimit);
            SetField(data, "stacksPerApplication", stacksPerApplication);
            SetField(data, "durationRefreshPolicy", E_EffectDurationRefresh.NeverRefresh);
            SetField(data, "periodResetPolicy", E_EffectPeriodReset.NeverReset);
            SetField(data, "expirationPolicy", E_EffectExpiration.ClearEntireStack);
            SetField(data, "statModifierConfig", new List<StatModifierConfig>
            {
                new StatModifierConfig { statId = "HP", type = E_ModifierType.FlatAdd, value = -1f }
            });
            return data;
        }

        /// <summary>
        /// 销毁 SO
        /// </summary>
        private static void DestroyAsset(UnityEngine.Object asset)
        {
            if (asset != null)
                Destroy(asset);
        }

        /// <summary>
        /// 反射写字段
        /// </summary>
        private static void SetField(object target, string fieldName, object value)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        #endregion

        #region Types

        /// <summary>
        /// 用例指标
        /// </summary>
        private readonly struct CaseMetrics
        {
            public readonly long ApplyMs;
            public readonly long UpdateMs;
            public readonly int AppliedCount;
            public readonly int Extra;

            public CaseMetrics(long applyMs, long updateMs, int appliedCount, int extra)
            {
                ApplyMs = applyMs;
                UpdateMs = updateMs;
                AppliedCount = appliedCount;
                Extra = extra;
            }
        }

        /// <summary>
        /// 用例结果
        /// </summary>
        private sealed class CaseResult
        {
            public string Name;
            public long ApplyMs;
            public long UpdateMs;
            public long TotalMs;
            public int AppliedCount;
            public int Extra;
            public long MemDeltaBytes;
            public int Gen0;
            public int Gen1;
            public int Gen2;
        }

        /// <summary>
        /// 单个压测目标
        /// </summary>
        private sealed class Target
        {
            public GameObject Root;
            public GEManager GE;
            public StatController Stats;
            private readonly List<UnityEngine.Object> assetList = new();

            /// <summary>
            /// 创建目标
            /// </summary>
            public static Target Create(string objectName)
            {
                Target target = new Target();
                target.Root = new GameObject(objectName);
                target.Stats = target.Root.AddComponent<StatController>();
                target.GE = target.Root.AddComponent<GEManager>();

                StatData hp = ScriptableObject.CreateInstance<StatData>();
                hp.name = "HP";
                SetField(hp, "statType", E_StatType.Immediate);
                SetField(hp, "baseValue", 100000f);
                SetField(hp, "minValue", 0f);
                SetField(hp, "maxValue", 100000f);
                SetField(hp, "resetCurrentValueOnPlay", true);
                target.assetList.Add(hp);

                SetField(target.Stats, "statDataList", new List<StatData> { hp });
                target.Stats.Init();
                target.GE.SetStatController(target.Stats);
                return target;
            }

            /// <summary>
            /// 销毁
            /// </summary>
            public void Dispose()
            {
                if (Root != null)
                    Destroy(Root);
                for (int i = 0; i < assetList.Count; i++)
                {
                    if (assetList[i] != null)
                        Destroy(assetList[i]);
                }
            }
        }

        #endregion
    }
}
