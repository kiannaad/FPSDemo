using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GAS.Core;
using GAS.Core.GameplayEffect;
using GAS.StateSystem;
using UnityEngine;

namespace GAS.Core.Effect.Test
{
    /// <summary>
    /// GE 堆叠与 UpdateGE 独立测试 挂到场景物体后 Play 即跑
    /// </summary>
    public class EffectStackingTester : MonoBehaviour
    {
        /// <summary> 是否在 Start 自动跑 </summary>
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
                RunAll();
        }

        /// <summary>
        /// 跑全部用例
        /// </summary>
        [ContextMenu("Run All Tests")]
        public void RunAll()
        {
            StringBuilder log = new StringBuilder();
            int pass = 0;
            int fail = 0;

            RunCase("组合一_可叠_重置周期", TestCombo1_Stack_ResetPeriod, log, ref pass, ref fail);
            RunCase("组合二_可叠_不重置周期", TestCombo2_Stack_KeepPeriod, log, ref pass, ref fail);
            RunCase("组合三_不叠_重置周期", TestCombo3_NoStack_ResetPeriod, log, ref pass, ref fail);
            RunCase("组合四_不叠_不重置周期", TestCombo4_NoStack_KeepPeriod, log, ref pass, ref fail);
            RunCase("UpdateGE_时长到期移除", TestUpdateGE_DurationExpire, log, ref pass, ref fail);
            RunCase("UpdateGE_满层仍续命", TestUpdateGE_MaxStackStillRefresh, log, ref pass, ref fail);
            RunCase("各层独立_错开时间线", TestIndependent_SeparateTimelines, log, ref pass, ref fail);
            RunCase("各层独立_满层拒绝", TestIndependent_RejectWhenFull, log, ref pass, ref fail);
            RunCase("共享_每次加2层", TestShared_StacksPerApplicationTwo, log, ref pass, ref fail);
            RunCase("独立_每次建2份", TestIndependent_StacksPerApplicationTwo, log, ref pass, ref fail);

            Debug.Log($"[EffectStackingTester] 完成 pass={pass} fail={fail}\n{log}");
        }

        #region 用例

        /// <summary>
        /// 可叠 + 重置周期 叠层后周期倒计时拉回满
        /// </summary>
        private void TestCombo1_Stack_ResetPeriod(Fixture fx)
        {
            var ge = fx.CreateDot(
                "C1",
                duration: 10f,
                period: 2f,
                stackLimit: 5,
                durationRefresh: E_EffectDurationRefresh.NeverRefresh,
                periodReset: E_EffectPeriodReset.ResetOnSuccessfulApplication,
                damage: -10f,
                executeOnApply: false);

            var spec = fx.Apply(ge);
            fx.Tick(1f);
            AssertApprox("周期已走1秒", spec.RemainingPeriod, 1f);

            fx.Apply(ge);
            AssertTrue("层数=2", spec.StackCount == 2);
            AssertApprox("重置后周期应回到2", spec.RemainingPeriod, 2f);
        }

        /// <summary>
        /// 可叠 + 不重置周期 层数涨但拍子不动
        /// </summary>
        private void TestCombo2_Stack_KeepPeriod(Fixture fx)
        {
            var ge = fx.CreateDot(
                "C2",
                duration: 4f,
                period: 2f,
                stackLimit: 5,
                durationRefresh: E_EffectDurationRefresh.NeverRefresh,
                periodReset: E_EffectPeriodReset.NeverReset,
                damage: -10f,
                executeOnApply: false);

            var spec = fx.Apply(ge);
            AssertApprox("初始寿命", spec.RemainingDuration, 4f);
            AssertApprox("初始周期", spec.RemainingPeriod, 2f);

            fx.Tick(1f);
            fx.Apply(ge);
            AssertTrue("层数=2", spec.StackCount == 2);
            AssertApprox("周期不重置仍约1", spec.RemainingPeriod, 1f);
            AssertApprox("不续命仍约3", spec.RemainingDuration, 3f);

            float hpBefore = fx.Hp;
            fx.Tick(1f);
            AssertApprox("t=2 跳伤×2层", fx.Hp, hpBefore - 20f);
        }

        /// <summary>
        /// 不叠 + 重置周期 续命且拍子重来
        /// </summary>
        private void TestCombo3_NoStack_ResetPeriod(Fixture fx)
        {
            var ge = fx.CreateDot(
                "C3",
                duration: 5f,
                period: 2f,
                stackLimit: 1,
                durationRefresh: E_EffectDurationRefresh.RefreshOnSuccessfulApplication,
                periodReset: E_EffectPeriodReset.ResetOnSuccessfulApplication,
                damage: -10f,
                executeOnApply: false);

            var spec = fx.Apply(ge);
            //分段推进 一次Tick(3)会越过跳伤点并ResetPeriod 测不到「还剩1秒」
            fx.Tick(1f);
            fx.Tick(1f);
            fx.Tick(1f);
            AssertApprox("再应用前寿命约2", spec.RemainingDuration, 2f);
            AssertApprox("再应用前周期约1", spec.RemainingPeriod, 1f);

            fx.Apply(ge);
            AssertTrue("层数仍1", spec.StackCount == 1);
            AssertApprox("续命回5", spec.RemainingDuration, 5f);
            AssertApprox("周期重置回2", spec.RemainingPeriod, 2f);
        }

        /// <summary>
        /// 不叠 + 不重置周期 续命但拍子保持
        /// </summary>
        private void TestCombo4_NoStack_KeepPeriod(Fixture fx)
        {
            var ge = fx.CreateDot(
                "C4",
                duration: 5f,
                period: 2f,
                stackLimit: 1,
                durationRefresh: E_EffectDurationRefresh.RefreshOnSuccessfulApplication,
                periodReset: E_EffectPeriodReset.NeverReset,
                damage: -10f,
                executeOnApply: false);

            var spec = fx.Apply(ge);
            fx.Tick(1f);
            fx.Tick(1f);
            fx.Tick(1f);
            AssertApprox("再应用前周期约1", spec.RemainingPeriod, 1f);

            fx.Apply(ge);
            AssertApprox("续命回5", spec.RemainingDuration, 5f);
            AssertApprox("周期保持约1", spec.RemainingPeriod, 1f);
        }

        /// <summary>
        /// 持续 Buff 到期后修饰符移除
        /// </summary>
        private void TestUpdateGE_DurationExpire(Fixture fx)
        {
            var ge = fx.CreateBuff(
                "ExpireBuff",
                duration: 2f,
                attackAdd: 40f);

            fx.Apply(ge);
            AssertApprox("Buff生效", fx.Attack, 140f);
            fx.Tick(2.1f);
            AssertApprox("到期移除", fx.Attack, 100f);
            AssertTrue("列表清空", fx.GE.AppliedCount == 0);
        }

        /// <summary>
        /// 满层再应用仍可续命 且不因重挂重复改值
        /// </summary>
        private void TestUpdateGE_MaxStackStillRefresh(Fixture fx)
        {
            var ge = fx.CreateBuff(
                "StackBuff",
                duration: 5f,
                attackAdd: 10f,
                stackLimit: 2,
                durationRefresh: E_EffectDurationRefresh.RefreshOnSuccessfulApplication);

            var spec = fx.Apply(ge);
            fx.Apply(ge);
            AssertTrue("满层=2", spec.StackCount == 2);
            AssertApprox("攻击+20", fx.Attack, 120f);

            fx.Tick(3f);
            AssertApprox("寿命约2", spec.RemainingDuration, 2f);

            fx.Apply(ge);
            AssertTrue("仍满层2", spec.StackCount == 2);
            AssertApprox("满层仍续命回5", spec.RemainingDuration, 5f);
            AssertApprox("满层续命不重复加攻", fx.Attack, 120f);
        }

        /// <summary>
        /// 各层独立 两份Runtime 周期互不影响
        /// </summary>
        private void TestIndependent_SeparateTimelines(Fixture fx)
        {
            var ge = fx.CreateDot(
                "Wound",
                duration: 4f,
                period: 2f,
                stackLimit: 5,
                durationRefresh: E_EffectDurationRefresh.NeverRefresh,
                periodReset: E_EffectPeriodReset.NeverReset,
                damage: -10f,
                executeOnApply: false,
                aggregation: E_EffectStackAggregation.IndependentInstances);

            var woundA = fx.Apply(ge);
            fx.Tick(1f);
            var woundB = fx.Apply(ge);

            AssertTrue("应有两份Runtime", fx.GE.CountEffect(ge) == 2);
            AssertTrue("不是同一份", !ReferenceEquals(woundA, woundB));
            AssertApprox("A周期已走1秒", woundA.RemainingPeriod, 1f);
            AssertApprox("B周期仍满2秒", woundB.RemainingPeriod, 2f);
            AssertApprox("A寿命约3", woundA.RemainingDuration, 3f);
            AssertApprox("B寿命满4", woundB.RemainingDuration, 4f);

            float hpBefore = fx.Hp;
            fx.Tick(1f);
            //仅A到跳伤点 各打10 不是共享层×2
            AssertApprox("仅A跳伤10", fx.Hp, hpBefore - 10f);
            AssertApprox("A跳后周期重置", woundA.RemainingPeriod, 2f);
            AssertApprox("B周期剩1", woundB.RemainingPeriod, 1f);
        }

        /// <summary>
        /// 各层独立 满层后再上拒绝
        /// </summary>
        private void TestIndependent_RejectWhenFull(Fixture fx)
        {
            var ge = fx.CreateDot(
                "WoundCap",
                duration: 5f,
                period: 2f,
                stackLimit: 2,
                durationRefresh: E_EffectDurationRefresh.NeverRefresh,
                periodReset: E_EffectPeriodReset.NeverReset,
                damage: -10f,
                executeOnApply: false,
                aggregation: E_EffectStackAggregation.IndependentInstances);

            AssertTrue("第1层成功", fx.Apply(ge) != null);
            AssertTrue("第2层成功", fx.Apply(ge) != null);
            AssertTrue("第3层拒绝", fx.Apply(ge) == null);
            AssertTrue("仍只有2份", fx.GE.CountEffect(ge) == 2);
        }

        /// <summary>
        /// 共享层数 每次应用+2
        /// </summary>
        private void TestShared_StacksPerApplicationTwo(Fixture fx)
        {
            var ge = fx.CreateBuff(
                "PlusTwo",
                duration: 5f,
                attackAdd: 10f,
                stackLimit: 5,
                stacksPerApplication: 2);

            var spec = fx.Apply(ge);
            AssertTrue("首次就是2层", spec.StackCount == 2);
            AssertApprox("攻击+20", fx.Attack, 120f);

            fx.Apply(ge);
            AssertTrue("再应用变4层", spec.StackCount == 4);
            AssertApprox("攻击+40", fx.Attack, 140f);
        }

        /// <summary>
        /// 各层独立 每次应用一次建2份
        /// </summary>
        private void TestIndependent_StacksPerApplicationTwo(Fixture fx)
        {
            var ge = fx.CreateDot(
                "DoubleWound",
                duration: 5f,
                period: 2f,
                stackLimit: 5,
                durationRefresh: E_EffectDurationRefresh.NeverRefresh,
                periodReset: E_EffectPeriodReset.NeverReset,
                damage: -10f,
                executeOnApply: false,
                aggregation: E_EffectStackAggregation.IndependentInstances,
                stacksPerApplication: 2);

            fx.Apply(ge);
            AssertTrue("一次建出2份", fx.GE.CountEffect(ge) == 2);
        }

        #endregion

        #region 断言与调度

        /// <summary>
        /// 跑单用例
        /// </summary>
        private void RunCase(string name, System.Action<Fixture> action, StringBuilder log, ref int pass, ref int fail)
        {
            Fixture fx = Fixture.Create();
            try
            {
                action(fx);
                pass++;
                log.AppendLine($"PASS {name}");
            }
            catch (System.Exception ex)
            {
                fail++;
                log.AppendLine($"FAIL {name}: {ex.Message}");
                Debug.LogError($"[EffectStackingTester] FAIL {name}\n{ex}");
            }
            finally
            {
                fx.Dispose();
            }
        }

        /// <summary>
        /// 近似相等
        /// </summary>
        private static void AssertApprox(string label, float actual, float expected, float eps = 0.05f)
        {
            if (Mathf.Abs(actual - expected) > eps)
                throw new System.Exception($"{label} 期望≈{expected} 实际={actual}");
        }

        /// <summary>
        /// 布尔
        /// </summary>
        private static void AssertTrue(string label, bool condition)
        {
            if (!condition)
                throw new System.Exception(label);
        }

        #endregion

        #region Fixture

        /// <summary>
        /// 最小测试台
        /// </summary>
        private sealed class Fixture
        {
            /// <summary> 根物体 </summary>
            public GameObject Root { get; private set; }

            /// <summary> GE管理器 </summary>
            public GEManager GE { get; private set; }

            /// <summary> 属性 </summary>
            public StatController Stats { get; private set; }

            /// <summary> 运行时资源 </summary>
            private readonly List<UnityEngine.Object> assetList = new();
    
            public float Hp => Stats.GetCurrentValue("HP");
            public float Attack => Stats.GetCurrentValue("Attack");

            /// <summary>
            /// 创建台子
            /// </summary>
            public static Fixture Create()
            {
                Fixture fx = new Fixture();
                fx.Root = new GameObject("EffectStackingFixture");
                fx.Stats = fx.Root.AddComponent<StatController>();
                fx.GE = fx.Root.AddComponent<GEManager>();

                StatData hp = CreateStat("HP", E_StatType.Immediate, 100f, 0f, 100f);
                StatData atk = CreateStat("Attack", E_StatType.Passive, 100f);
                fx.assetList.Add(hp);
                fx.assetList.Add(atk);

                SetField(fx.Stats, "statDataList", new List<StatData> { hp, atk });
                fx.Stats.Init();
                fx.GE.SetStatController(fx.Stats);
                return fx;
            }

            /// <summary>
            /// 推进时间
            /// </summary>
            public void Tick(float dt) => GE.UpdateGE(dt);

            /// <summary>
            /// 应用GE
            /// </summary>
            public GameplayEffectRuntime Apply(GameplayEffectData data) => GE.ApplyGE(data, Root);

            /// <summary>
            /// 创建周期伤害GE
            /// </summary>
            public GameplayEffectData CreateDot(
                string name,
                float duration,
                float period,
                int stackLimit,
                E_EffectDurationRefresh durationRefresh,
                E_EffectPeriodReset periodReset,
                float damage,
                bool executeOnApply = true,
                E_EffectStackAggregation aggregation = E_EffectStackAggregation.SharedCount,
                int stacksPerApplication = 1)
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
                SetField(data, "durationRefreshPolicy", durationRefresh);
                SetField(data, "periodResetPolicy", periodReset);
                SetField(data, "expirationPolicy", E_EffectExpiration.ClearEntireStack);
                SetField(data, "statModifierConfig", new List<StatModifierConfig>
                {
                    new StatModifierConfig { statId = "HP", type = E_ModifierType.FlatAdd, value = damage }
                });
                assetList.Add(data);
                return data;
            }

            /// <summary>
            /// 创建持续攻击Buff
            /// </summary>
            public GameplayEffectData CreateBuff(
                string name,
                float duration,
                float attackAdd,
                int stackLimit = 1,
                int stacksPerApplication = 1,
                E_EffectDurationRefresh durationRefresh = E_EffectDurationRefresh.RefreshOnSuccessfulApplication)
            {
                GameplayEffectData data = ScriptableObject.CreateInstance<GameplayEffectData>();
                data.name = name;
                SetField(data, "durationPolicy", E_EffectDuration.HasDuration);
                SetField(data, "duration", duration);
                SetField(data, "isPeriodic", false);
                SetField(data, "stackAggregation", E_EffectStackAggregation.SharedCount);
                SetField(data, "stackLimit", stackLimit);
                SetField(data, "stacksPerApplication", stacksPerApplication);
                SetField(data, "durationRefreshPolicy", durationRefresh);
                SetField(data, "periodResetPolicy", E_EffectPeriodReset.NeverReset);
                SetField(data, "expirationPolicy", E_EffectExpiration.ClearEntireStack);
                SetField(data, "statModifierConfig", new List<StatModifierConfig>
                {
                    new StatModifierConfig { statId = "Attack", type = E_ModifierType.FlatAdd, value = attackAdd }
                });
                assetList.Add(data);
                return data;
            }

            /// <summary>
            /// 销毁
            /// </summary>
            public void Dispose()
            {
                if (Root != null)
                    UnityEngine.Object.Destroy(Root);
                for (int i = 0; i < assetList.Count; i++)
                {
                    if (assetList[i] != null)
                        UnityEngine.Object.Destroy(assetList[i]);
                }
            }

            /// <summary>
            /// 创建属性数据
            /// </summary>
            private static StatData CreateStat(string id, E_StatType type, float baseValue, float min = float.MinValue, float max = float.MaxValue)
            {
                StatData data = ScriptableObject.CreateInstance<StatData>();
                data.name = id;
                SetField(data, "statType", type);
                SetField(data, "baseValue", baseValue);
                SetField(data, "minValue", min);
                SetField(data, "maxValue", max);
                SetField(data, "resetCurrentValueOnPlay", true);
                return data;
            }

            /// <summary>
            /// 反射写私有字段
            /// </summary>
            private static void SetField(object target, string fieldName, object value)
            {
                System.Type type = target.GetType();
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
        }

        #endregion
    }
}
