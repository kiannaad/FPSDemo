using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GAS.AbilitySystem;
using GAS.Component;
using GAS.Core;
using GAS.Core.GameplayEffect;
using GAS.StateSystem;
using UnityEngine;

namespace GAS.Demo
{
    /// <summary>
    /// GAS 整体交互 Demo 空场景挂本脚本后 Play 即可
    /// 自动搭 Stat + GE + Ability 闭环 左上角 OnGUI 操作
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class GasDemoController : MonoBehaviour
    {
        /// <summary> 是否 Start 自动搭建 </summary>
        [SerializeField]
        private bool buildOnStart = true;

        /// <summary> 是否显示 OnGUI </summary>
        [SerializeField]
        private bool showGui = true;

        /// <summary> 属性 </summary>
        private StatController mStats;

        /// <summary> GE </summary>
        private GEManager mGe;

        /// <summary> ASC </summary>
        private AbilitySystemMgr mAsc;

        /// <summary> 运行时创建的临时资产 </summary>
        private readonly List<Object> mAssetList = new List<Object>();

        /// <summary> 最近操作提示 </summary>
        private string mLastHint = "按 1 火球 / 2 战吼 / 3 叠毒 或点按钮";

        /// <summary> GUI 区域 </summary>
        private Rect mWindowRect = new Rect(12f, 12f, 420f, 360f);

        private void Awake()
        {
            mStats = GetComponent<StatController>();
            if (mStats == null)
                mStats = gameObject.AddComponent<StatController>();

            mGe = GetComponent<GEManager>();
            if (mGe == null)
                mGe = gameObject.AddComponent<GEManager>();

            mAsc = GetComponent<AbilitySystemMgr>();
            if (mAsc == null)
                mAsc = gameObject.AddComponent<AbilitySystemMgr>();

            SetPrivateField(mAsc, "geManager", mGe);
            SetPrivateField(mAsc, "statController", mStats);
            SetPrivateField(mAsc, "initialAbilities", new List<GameplayAbilityData>());
        }

        private void Start()
        {
            if (buildOnStart)
                BuildDemo();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                TryCast("Fireball");
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                TryCast("WarCry");
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                TryCast("Poison");
            if (Input.GetKeyDown(KeyCode.R))
                ResetVitals();
            if (Input.GetKeyDown(KeyCode.T))
                ToggleReadyTag();
        }

        private void OnDestroy()
        {
            DisposeAssets();
        }

        /// <summary>
        /// 释放运行时资产
        /// </summary>
        private void DisposeAssets()
        {
            for (int i = 0; i < mAssetList.Count; i++)
            {
                if (mAssetList[i] != null)
                    Destroy(mAssetList[i]);
            }
            mAssetList.Clear();
        }

        private void OnGUI()
        {
            if (!showGui) return;
            mWindowRect = GUI.Window(GetInstanceID(), mWindowRect, DrawWindow, "GAS Demo");
        }

        /// <summary>
        /// 搭建属性技能与效果
        /// </summary>
        [ContextMenu("Rebuild Demo")]
        public void BuildDemo()
        {
            mAsc.ClearAbilities();
            mAsc.RemoveAllGE();
            DisposeAssets();

            StatData hp = CreateStat("HP", E_StatType.Immediate, 100f, 0f, 100f);
            StatData mp = CreateStat("MP", E_StatType.Immediate, 100f, 0f, 100f);
            StatData attack = CreateStat("Attack", E_StatType.Passive, 20f);

            SetPrivateField(mStats, "statDataList", new List<StatData> { hp, mp, attack });
            mStats.Init();

            mGe.SetStatController(mStats);
            mGe.SetOwner(mAsc);

            GameplayEffectData geDamage = CreateInstantDamage("GE_Demo_Damage", "HP", -25f);
            GameplayEffectData geBuff = CreateDurationBuff("GE_Demo_WarCry", "Attack", 15f, 8f, stackLimit: 3);
            GameplayEffectData geDot = CreatePoisonDot("GE_Demo_Poison", "HP", -8f, duration: 6f, period: 1.5f);

            GameplayAbilityData fireball = CreateAbility(
                "Fireball",
                cooldown: 2f,
                costStatId: "MP",
                costValue: 30f,
                needTags: new[] { "State.Ready" },
                effects: new[] { geDamage });

            GameplayAbilityData warCry = CreateAbility(
                "WarCry",
                cooldown: 4f,
                costStatId: "MP",
                costValue: 20f,
                needTags: new[] { "State.Ready" },
                effects: new[] { geBuff });

            GameplayAbilityData poison = CreateAbility(
                "Poison",
                cooldown: 1f,
                costStatId: "MP",
                costValue: 15f,
                needTags: new[] { "State.Ready" },
                effects: new[] { geDot });

            mAsc.GrantAbility(fireball);
            mAsc.GrantAbility(warCry);
            mAsc.GrantAbility(poison);
            mAsc.AddGameplayTag("State.Ready");

            mLastHint = "Demo 已就绪 按 1/2/3 放技能";
            Debug.Log("[GasDemo] 搭建完成 HP/MP/Attack + Fireball/WarCry/Poison");
        }

        /// <summary>
        /// 尝试放技能
        /// </summary>
        private void TryCast(string abilityName)
        {
            bool ok = mAsc.TryActivateAbility(abilityName);
            mLastHint = ok
                ? $"激活成功 {abilityName}"
                : $"激活失败 {abilityName} 查 CD/蓝/标签";
        }

        /// <summary>
        /// 回满血蓝
        /// </summary>
        private void ResetVitals()
        {
            SetImmediate("HP", 100f);
            SetImmediate("MP", 100f);
            mLastHint = "已回满 HP/MP";
        }

        /// <summary>
        /// 切换 Ready 标签
        /// </summary>
        private void ToggleReadyTag()
        {
            if (mAsc.HasGameplayTag("State.Ready"))
            {
                mAsc.RemoveGameplayTag("State.Ready");
                mLastHint = "已移除 State.Ready 技能会被拦截";
            }
            else
            {
                mAsc.AddGameplayTag("State.Ready");
                mLastHint = "已添加 State.Ready";
            }
        }

        /// <summary>
        /// 画窗口
        /// </summary>
        private void DrawWindow(int id)
        {
            GUILayout.Label(mLastHint);
            GUILayout.Space(4f);

            if (mStats != null)
            {
                GUILayout.Label(
                    $"HP {mStats.GetCurrentValue("HP"):0.#}   " +
                    $"MP {mStats.GetCurrentValue("MP"):0.#}   " +
                    $"ATK {mStats.GetCurrentValue("Attack"):0.#}");
            }

            GUILayout.Label($"Tag Ready={(mAsc != null && mAsc.HasGameplayTag("State.Ready"))}");
            GUILayout.Label($"GE 数量={(mGe != null ? mGe.AppliedCount : 0)}");

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 火球"))
                TryCast("Fireball");
            if (GUILayout.Button("2 战吼"))
                TryCast("WarCry");
            if (GUILayout.Button("3 叠毒"))
                TryCast("Poison");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("R 回满"))
                ResetVitals();
            if (GUILayout.Button("T Ready开关"))
                ToggleReadyTag();
            if (GUILayout.Button("清GE"))
            {
                mAsc.RemoveAllGE();
                mLastHint = "已清除全部 GE";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("技能状态", GUI.skin.box);
            if (mAsc != null)
            {
                IReadOnlyList<AbilityRuntime> abilityList = mAsc.ActivatableAbilities;
                for (int i = 0; i < abilityList.Count; i++)
                {
                    AbilityRuntime spec = abilityList[i];
                    if (spec?.ability == null) continue;
                    string cdText = spec.cooldown != null && spec.cooldown.IsOncooldown
                        ? $"CD {spec.cooldown.RamingCooldown:0.0}s"
                        : "就绪";
                    GUILayout.Label($"{spec.ability.abilityName}  [{cdText}]");
                }
            }

            GUILayout.Space(4f);
            GUILayout.Label("在途 GE", GUI.skin.box);
            if (mGe != null)
            {
                IReadOnlyList<GameplayEffectRuntime> geList = mGe.AppliedEffects;
                if (geList.Count == 0)
                {
                    GUILayout.Label("(无)");
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < geList.Count; i++)
                    {
                        GameplayEffectRuntime runtime = geList[i];
                        if (runtime?.GEData == null) continue;
                        sb.Append(runtime.GEData.name);
                        sb.Append(" ×");
                        sb.Append(runtime.StackCount);
                        if (runtime.GEData.HasDuration)
                        {
                            sb.Append(" 剩");
                            sb.Append(runtime.RemainingDuration.ToString("0.0"));
                            sb.Append('s');
                        }
                        GUILayout.Label(sb.ToString());
                        sb.Clear();
                    }
                }
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        /// <summary>
        /// 设置即时属性到目标值
        /// </summary>
        private void SetImmediate(string statId, float value)
        {
            IImmediateStat imStat = mStats.GetImStat(statId);
            if (imStat == null) return;
            float delta = value - imStat.CurrentValue;
            imStat.ChangeValue(delta, E_ModifierType.FlatAdd);
        }

        /// <summary>
        /// 创建技能
        /// </summary>
        private GameplayAbilityData CreateAbility(
            string abilityName,
            float cooldown,
            string costStatId,
            float costValue,
            string[] needTags,
            GameplayEffectData[] effects)
        {
            GameplayAbilityData ability = ScriptableObject.CreateInstance<GameplayAbilityData>();
            ability.name = abilityName;
            ability.abilityName = abilityName;
            ability.cooldownTime = cooldown;
            ability.costStatId = costStatId;
            ability.costType = E_CostType.Fixed;
            ability.costValue = costValue;
            ability.description = $"Demo 技能 {abilityName}";

            SetPrivateField(ability, "activationRequiredTags", needTags ?? System.Array.Empty<string>());
            SetPrivateField(ability, "activationBlockedTags", System.Array.Empty<string>());

            if (effects != null)
            {
                List<GameplayEffectData> effectList = new List<GameplayEffectData>(effects);
                SetPrivateField(ability, "applyEffectList", effectList);
            }

            mAssetList.Add(ability);
            return ability;
        }

        /// <summary>
        /// 即时伤害
        /// </summary>
        private GameplayEffectData CreateInstantDamage(string name, string statId, float value)
        {
            GameplayEffectData data = ScriptableObject.CreateInstance<GameplayEffectData>();
            data.name = name;
            SetPrivateField(data, "durationPolicy", E_EffectDuration.Instant);
            SetPrivateField(data, "statModifierConfig", new List<StatModifierConfig>
            {
                new StatModifierConfig { statId = statId, type = E_ModifierType.FlatAdd, value = value }
            });
            mAssetList.Add(data);
            return data;
        }

        /// <summary>
        /// 持续攻击 Buff 可叠
        /// </summary>
        private GameplayEffectData CreateDurationBuff(
            string name,
            string statId,
            float addValue,
            float duration,
            int stackLimit)
        {
            GameplayEffectData data = ScriptableObject.CreateInstance<GameplayEffectData>();
            data.name = name;
            SetPrivateField(data, "durationPolicy", E_EffectDuration.HasDuration);
            SetPrivateField(data, "duration", duration);
            SetPrivateField(data, "stackAggregation", E_EffectStackAggregation.SharedCount);
            SetPrivateField(data, "stackLimit", stackLimit);
            SetPrivateField(data, "stacksPerApplication", 1);
            SetPrivateField(data, "durationRefreshPolicy", E_EffectDurationRefresh.RefreshOnSuccessfulApplication);
            SetPrivateField(data, "statModifierConfig", new List<StatModifierConfig>
            {
                new StatModifierConfig { statId = statId, type = E_ModifierType.FlatAdd, value = addValue }
            });
            mAssetList.Add(data);
            return data;
        }

        /// <summary>
        /// 毒 Dot 共享层
        /// </summary>
        private GameplayEffectData CreatePoisonDot(
            string name,
            string statId,
            float damage,
            float duration,
            float period)
        {
            GameplayEffectData data = ScriptableObject.CreateInstance<GameplayEffectData>();
            data.name = name;
            SetPrivateField(data, "durationPolicy", E_EffectDuration.HasDuration);
            SetPrivateField(data, "duration", duration);
            SetPrivateField(data, "isPeriodic", true);
            SetPrivateField(data, "period", period);
            SetPrivateField(data, "executePeriodicEffectOnApplication", true);
            SetPrivateField(data, "stackAggregation", E_EffectStackAggregation.SharedCount);
            SetPrivateField(data, "stackLimit", 5);
            SetPrivateField(data, "stacksPerApplication", 1);
            SetPrivateField(data, "durationRefreshPolicy", E_EffectDurationRefresh.RefreshOnSuccessfulApplication);
            SetPrivateField(data, "periodResetPolicy", E_EffectPeriodReset.ResetOnSuccessfulApplication);
            SetPrivateField(data, "statModifierConfig", new List<StatModifierConfig>
            {
                new StatModifierConfig { statId = statId, type = E_ModifierType.FlatAdd, value = damage }
            });
            mAssetList.Add(data);
            return data;
        }

        /// <summary>
        /// 创建属性定义
        /// </summary>
        private StatData CreateStat(
            string id,
            E_StatType type,
            float baseValue,
            float min = float.MinValue,
            float max = float.MaxValue)
        {
            StatData data = ScriptableObject.CreateInstance<StatData>();
            data.name = id;
            SetPrivateField(data, "statType", type);
            SetPrivateField(data, "baseValue", baseValue);
            SetPrivateField(data, "minValue", min);
            SetPrivateField(data, "maxValue", max);
            SetPrivateField(data, "resetCurrentValueOnPlay", true);
            mAssetList.Add(data);
            return data;
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
            throw new System.MissingFieldException(target.GetType().Name, fieldName);
        }
    }
}
