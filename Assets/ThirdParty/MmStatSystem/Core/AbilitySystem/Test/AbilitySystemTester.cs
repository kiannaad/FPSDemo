using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GAS.AbilitySystem;
using GAS.Component;
using GAS.Core;
using GAS.StateSystem;
using UnityEngine;

namespace GAS.AbilitySystem.Test
{
    /// <summary>
    /// AbilitySystem 独立测试 挂场景物体后 Play 即跑 不依赖临时 SO
    /// </summary>
    public class AbilitySystemTester : MonoBehaviour
    {
        /// <summary> 是否 Start 自动跑 </summary>
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

            RunCase("冷却拦截二次激活", TestCooldownBlocksSecondActivate, log, ref pass, ref fail);
            RunCase("消耗不足拦截", TestCostInsufficientBlocks, log, ref pass, ref fail);
            RunCase("消耗成功扣除", TestCostAppliesOnActivate, log, ref pass, ref fail);
            RunCase("标签条件拦截与通过", TestTagRequirements, log, ref pass, ref fail);
            RunCase("中断上下文", TestInterruptCancelsContext, log, ref pass, ref fail);
            RunCase("同SO多Spec不串CD", TestSharedSoIndependentCooldown, log, ref pass, ref fail);

            Debug.Log($"[AbilitySystemTester] 完成 pass={pass} fail={fail}\n{log}");
        }

        #region Cases

        /// <summary>
        /// CD 中不可再激活
        /// </summary>
        private void TestCooldownBlocksSecondActivate(Fixture fx)
        {
            var ability = fx.CreateAbility("CDAbility", cooldown: 2f);
            AbilityRuntime spec = ability.CreateAbilitySpec();

            AssertTrue("首次可激活", spec.CanActivate(fx.Stats, fx.ASC));
            spec.Activate(fx.ASC, fx.Stats);
            AssertTrue("激活计数1", ability.ActivateCount == 1);
            AssertTrue("冷却中拦截", !spec.CanActivate(fx.Stats, fx.ASC));

            spec.cooldown.UpdateCooldown(true, 2.1f);
            AssertTrue("CD结束后可再激活", spec.CanActivate(fx.Stats, fx.ASC));
        }

        /// <summary>
        /// MP 不足不可激活
        /// </summary>
        private void TestCostInsufficientBlocks(Fixture fx)
        {
            var ability = fx.CreateAbility(
                "CostBlock",
                cooldown: 0f,
                costStatId: "MP",
                costType: E_CostType.Fixed,
                costValue: 50f);
            AbilityRuntime spec = ability.CreateAbilitySpec();

            fx.SetMp(30f);
            AssertTrue("MP不足拦截", !spec.CanActivate(fx.Stats, fx.ASC));
            AssertTrue("未激活", ability.ActivateCount == 0);
        }

        /// <summary>
        /// 激活成功扣蓝
        /// </summary>
        private void TestCostAppliesOnActivate(Fixture fx)
        {
            var ability = fx.CreateAbility(
                "CostOk",
                cooldown: 0f,
                costStatId: "MP",
                costType: E_CostType.Fixed,
                costValue: 40f);
            AbilityRuntime spec = ability.CreateAbilitySpec();

            fx.SetMp(100f);
            AssertTrue("可激活", spec.CanActivate(fx.Stats, fx.ASC));
            spec.Activate(fx.ASC, fx.Stats);
            AssertApprox("MP扣到60", fx.Mp, 60f);
            AssertTrue("激活计数1", ability.ActivateCount == 1);
        }

        /// <summary>
        /// 需要/禁止标签
        /// </summary>
        private void TestTagRequirements(Fixture fx)
        {
            var ability = fx.CreateAbility("TagAbility", cooldown: 0f);
            SetPrivateField(ability, "activationRequiredTags", new[] { "State.Ready" });
            SetPrivateField(ability, "activationBlockedTags", new[] { "State.Stunned" });
            AbilityRuntime spec = ability.CreateAbilitySpec();

            AssertTrue("缺Need拦截", !spec.CanActivate(fx.Stats, fx.ASC));

            fx.ASC.AddGameplayTag("State.Ready");
            AssertTrue("有Need可通过", spec.CanActivate(fx.Stats, fx.ASC));

            fx.ASC.AddGameplayTag("State.Stunned");
            AssertTrue("有Ban拦截", !spec.CanActivate(fx.Stats, fx.ASC));
        }

        /// <summary>
        /// Interrupt 标记上下文取消
        /// </summary>
        private void TestInterruptCancelsContext(Fixture fx)
        {
            var ability = fx.CreateAbility("CancelAbility", cooldown: 0f);
            AbilityRuntime spec = ability.CreateAbilitySpec();
            spec.Activate(fx.ASC, fx.Stats);
            spec.Interrupt();

            AssertTrue("上下文非空", ability.LastContext != null);
            AssertTrue("已取消", ability.LastContext.IsCancelled);
        }

        /// <summary>
        /// 同一 SO 两份 Spec CD 互不干扰
        /// </summary>
        private void TestSharedSoIndependentCooldown(Fixture fx)
        {
            var ability = fx.CreateAbility("SharedSO", cooldown: 5f);
            AbilityRuntime specA = ability.CreateAbilitySpec();
            AbilityRuntime specB = ability.CreateAbilitySpec();

            specA.Activate(fx.ASC, fx.Stats);
            AssertTrue("A进CD", specA.cooldown.IsOncooldown);
            AssertTrue("B未进CD", !specB.cooldown.IsOncooldown);
            AssertTrue("B仍可激活", specB.CanActivate(fx.Stats, fx.ASC));
        }

        #endregion

        #region Runner

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
                Debug.LogError($"[AbilitySystemTester] FAIL {name}\n{ex}");
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

        /// <summary>
        /// 反射写私有字段
        /// </summary>
        private static void SetPrivateField(object target, string fieldName, object value)
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

        #endregion

        #region Fixture

        /// <summary>
        /// 运行时测试技能 不落盘
        /// </summary>
        private sealed class TestAbility : GameplayAbilityData
        {
            /// <summary> 激活次数 </summary>
            public int ActivateCount;

            /// <summary> 最近一次上下文 </summary>
            public AbilityContext LastContext;

            /// <summary>
            /// 激活
            /// </summary>
            public override void Activate(AbilityContext context)
            {
                ActivateCount++;
                LastContext = context;
            }
        }

        /// <summary>
        /// 最小测试台
        /// </summary>
        private sealed class Fixture
        {
            public GameObject Root { get; private set; }
            public StatController Stats { get; private set; }
            public AbilitySystemMgr ASC { get; private set; }
            public GEManager GE { get; private set; }

            /// <summary> 运行时资源 </summary>
            private readonly List<UnityEngine.Object> assetList = new();

            public float Mp => Stats.GetCurrentValue("MP");

            /// <summary>
            /// 创建台子
            /// </summary>
            public static Fixture Create()
            {
                Fixture fx = new Fixture();
                fx.Root = new GameObject("AbilitySystemFixture");
                fx.Stats = fx.Root.AddComponent<StatController>();
                fx.GE = fx.Root.AddComponent<GEManager>();
                fx.ASC = fx.Root.AddComponent<AbilitySystemMgr>();

                StatData mp = CreateStat("MP", E_StatType.Immediate, 100f, 0f, 100f);
                fx.assetList.Add(mp);

                SetField(fx.Stats, "statDataList", new List<StatData> { mp });
                SetField(fx.ASC, "geManager", fx.GE);
                SetField(fx.ASC, "statController", fx.Stats);
                SetField(fx.ASC, "initialAbilities", new List<GameplayAbilityData>());

                fx.Stats.Init();
                fx.GE.SetStatController(fx.Stats);
                fx.GE.SetOwner(fx.ASC);
                return fx;
            }

            /// <summary>
            /// 设置 MP
            /// </summary>
            public void SetMp(float value)
            {
                var mp = Stats.GetImStat("MP");
                float delta = value - mp.CurrentValue;
                mp.ChangeValue(delta, E_ModifierType.FlatAdd);
            }

            /// <summary>
            /// 创建运行时技能配置
            /// </summary>
            public TestAbility CreateAbility(
                string name,
                float cooldown,
                string costStatId = "",
                E_CostType costType = E_CostType.Fixed,
                float costValue = 0f)
            {
                TestAbility ability = ScriptableObject.CreateInstance<TestAbility>();
                ability.name = name;
                ability.abilityName = name;
                ability.cooldownTime = cooldown;
                ability.costStatId = costStatId;
                ability.costType = costType;
                ability.costValue = costValue;
                assetList.Add(ability);
                return ability;
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
            private static StatData CreateStat(
                string id,
                E_StatType type,
                float baseValue,
                float min,
                float max)
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
            /// 反射写字段
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
