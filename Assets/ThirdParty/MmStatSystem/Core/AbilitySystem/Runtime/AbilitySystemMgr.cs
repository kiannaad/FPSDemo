using System;
using System.Collections.Generic;
using System.Threading;
using GAS.AbilitySystem;
using GAS.Core;
using GAS.Core.GameplayEffect;
using GAS.StateSystem;
using GAS.TagSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GAS.Component
{
    /// <summary>
    /// 能力系统组件 - 挂载在角色身上
    /// 只管理技能（Abilities），GE 交给 GEManager
    /// </summary>
    public class AbilitySystemMgr : MonoBehaviour
    {
        [Header("组件依赖")]
        [LabelText("GE管理器"), SerializeField] private GEManager geManager;
        [LabelText("属性控制器"), SerializeField] private StatController statController;

        [Header("技能配置")]
        [LabelText("初始技能"), SerializeField] private List<GameplayAbilityData> initialAbilities;

        [Header("标签配置")]
        [HideInInspector]
        [SerializeField] private string[] initialGameplayTags = Array.Empty<string>();

        //可激活的技能列表
        private readonly List<AbilityRuntime> activatableAbilities = new();

        /// <summary>
        /// 当前持有标签
        /// </summary>
        private readonly GameplayTagContainer gameplayTagContainer = new();

        /// <summary>
        /// 当前持有标签
        /// </summary>
        public GameplayTagContainer GameplayTags => gameplayTagContainer;

        /// <summary>
        /// 属性控制器
        /// </summary>
        public StatController StatController => statController;

        /// <summary>
        /// GE 管理器
        /// </summary>
        public GEManager GeManager => geManager;

        /// <summary>
        /// 可激活技能列表
        /// </summary>
        public IReadOnlyList<AbilityRuntime> ActivatableAbilities => activatableAbilities;

        // CancellationToken
        private CancellationTokenSource _cts;

        void Awake()
        {
            statController ??= GetComponent<StatController>();
            geManager ??= GetComponent<GEManager>();
            _cts = new CancellationTokenSource();
            InitGameplayTags();
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        /// <summary>
        /// 获取CancellationToken（用于UniTask）
        /// </summary>
        public CancellationToken GetCancellationTokenOnDestroy()
        {
            return _cts?.Token ?? default;
        }

        void Start()
        {
            // 初始化 GEManager
            if (geManager is not null)
            {
                geManager.SetStatController(statController);
                geManager.SetOwner(this);
            }

            //初始化技能配置
            if (initialAbilities == null) return;
            for (int i = 0; i < initialAbilities.Count; i++)
            {
                GameplayAbilityData ability = initialAbilities[i];
                if (ability == null) continue;
                GrantAbility(ability);
            }
        }

        void Update()
        {
            // 更新 GE
            geManager?.UpdateGE(Time.deltaTime);

            // 更新技能冷却
            foreach (var spec in activatableAbilities)
            {
                spec.cooldown?.UpdateCooldown(true, Time.deltaTime);
            }
        }

        #region 技能管理

        /// <summary>
        /// 运行时授予技能
        /// </summary>
        public AbilityRuntime GrantAbility(GameplayAbilityData ability)
        {
            if (ability == null) return null;

            AbilityRuntime abilitySpec = ability.CreateAbilitySpec();
            activatableAbilities.Add(abilitySpec);
            return abilitySpec;
        }

        /// <summary>
        /// 清空已授予技能
        /// </summary>
        public void ClearAbilities()
        {
            activatableAbilities.Clear();
        }

        /// <summary>
        /// 尝试激活技能
        /// </summary>
        public bool TryActivateAbility(string abilityName, StatController stat)
        {
            Debug.Log($"[ASC] TryActivateAbility - abilityName: {abilityName}");
            
            //从激活列表中找到技能配置
            foreach (var spec in activatableAbilities)
            {
                Debug.Log($"[ASC] 检查技能: {spec.ability.abilityName}");
                
                //匹配技能名称 并且可以激活
                if (spec.ability.abilityName == abilityName)
                {
                    Debug.Log($"[ASC] 找到匹配技能: {abilityName}, 检查CanActivate...");
                    if (spec.CanActivate(stat, this))
                    {
                        Debug.Log($"[ASC] CanActivate通过，准备激活!");
                        spec.Activate(this, stat);
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"[ASC] CanActivate失败，技能无法激活");
                    }
                }
            }
            Debug.LogWarning($"[ASC] 未找到可激活的技能: {abilityName}");
            return false;
        }

        /// <summary>
        /// 尝试激活技能
        /// </summary>
        public bool TryActivateAbility(string abilityName)
        {
            return TryActivateAbility(abilityName, statController);
        }

        #endregion

        #region 标签管理

        /// <summary>
        /// 初始化标签
        /// </summary>
        private void InitGameplayTags()
        {
            gameplayTagContainer.Clear();

            foreach (string tagName in initialGameplayTags)
            {
                AddGameplayTag(tagName);
            }
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        public void AddGameplayTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return;

            gameplayTagContainer.AddTag(new GameplayTag(tagName));
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public void RemoveGameplayTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return;

            gameplayTagContainer.RemoveTag(new GameplayTag(tagName));
        }

        /// <summary>
        /// 是否持有标签
        /// </summary>
        public bool HasGameplayTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return false;

            return gameplayTagContainer.ContainsTag(new GameplayTag(tagName));
        }

        /// <summary>
        /// 是否满足标签需求
        /// </summary>
        public bool SatisfiesTagRequirements(IEnumerable<string> needTags, IEnumerable<string> banTags)
        {
            foreach (string tagName in needTags)
            {
                if (string.IsNullOrWhiteSpace(tagName)) continue;
                if (!HasGameplayTag(tagName)) return false;
            }

            foreach (string tagName in banTags)
            {
                if (string.IsNullOrWhiteSpace(tagName)) continue;
                if (HasGameplayTag(tagName)) return false;
            }

            return true;
        }

        #endregion

        #region GE转发

        /// <summary>
        /// 应用GE（转发到GEManager）
        /// </summary>
        public GameplayEffectRuntime ApplyGE(GameplayEffectData effectData, object source)
        {
            if (geManager is null)
            {
                Debug.LogWarning("GEManager 未配置，无法应用 GE");
                return null;
            }
            return geManager.ApplyGE(effectData, source);
        }

        /// <summary>
        /// 移除所有GE
        /// </summary>
        public void RemoveAllGE()
        {
            geManager?.RemoveAllGE();
        }

        #endregion
    }
}
