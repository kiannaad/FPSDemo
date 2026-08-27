using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame
{
    [CreateAssetMenu(menuName = "CGame/Input/Input Profile")]
    public sealed class InputProfile : ScriptableObject
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapId;
        [SerializeField] private InputTagConfig inputTagConfig;

        public InputTagConfig InputTagConfig => inputTagConfig;

        public void SetConfiguration(InputActionAsset inputActions, InputActionMap actionMap, InputTagConfig inputTagConfig)
        {
            this.inputActions = inputActions;
            actionMapId = actionMap?.id.ToString();
            this.inputTagConfig = inputTagConfig;
        }

        public bool TryValidate(out string error)
        {
            InputActionMap actionMap = ResolveActionMap();
            if (actionMap == null || inputTagConfig == null)
            {
                error = "InputProfile requires an ActionMap and InputTagConfig.";
                return false;
            }

            var actionIds = new HashSet<Guid>();
            var inputTags = new HashSet<GameplayTag>();
            foreach (InputTagBinding binding in inputTagConfig.Bindings)
            {
                InputAction action = binding?.ActionReference?.action;
                if (action == null || action.actionMap == null || action.actionMap.id != actionMap.id)
                {
                    error = "Every InputTag binding must reference an action in the configured ActionMap.";
                    return false;
                }

                if (action.type != InputActionType.Button)
                {
                    error = "InputTag bindings only support Button actions.";
                    return false;
                }

                if (!IsExplicitLeafTag(binding.InputTag))
                {
                    error = "Every InputTag binding requires a registered explicit leaf GameplayTag.";
                    return false;
                }

                if (!actionIds.Add(action.id) || !inputTags.Add(binding.InputTag))
                {
                    error = "InputTag bindings cannot repeat an action or InputTag.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public bool TryResolveActionMap(out InputActionMap actionMap)
        {
            actionMap = ResolveActionMap();
            return actionMap != null;
        }

        public bool TryResolveAction(string actionName, out InputAction action)
        {
            InputActionMap actionMap = ResolveActionMap();
            action = actionMap?.FindAction(actionName, false);
            return action != null;
        }

        private InputActionMap ResolveActionMap()
        {
            return inputActions != null && Guid.TryParse(actionMapId, out Guid mapId)
                ? inputActions.actionMaps.FirstOrDefault(map => map.id == mapId)
                : null;
        }

        private static bool IsExplicitLeafTag(GameplayTag tag)
        {
            return !tag.IsEmpty && GameplayTagManager.Instance.IsExplicitTag(tag) && GameplayTagManager.Instance.GetDirectChildren(tag).Count == 0;
        }
    }
}
