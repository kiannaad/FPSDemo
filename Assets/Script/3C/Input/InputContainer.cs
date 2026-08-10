using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace CGame
{
    public abstract class InputContainer
    {
        private readonly List<StateCallbackRegistration> _stateCallbackRegistrations = new List<StateCallbackRegistration>();

        protected abstract InputActionMap ActionMap { get; }

        /// <summary>
        /// 启用当前输入方案。
        /// </summary>
        public void Enable() => ActionMap.Enable();

        /// <summary>
        /// 禁用当前输入方案，并清理回调注册和输入状态。
        /// </summary>
        public void Disable()
        {
            ClearingCallbacks();
            ActionMap.Disable();
            ClearingState();
        }

        /// <summary>
        /// 根据输入状态语义启用对应的底层 Action。
        /// </summary>
        public void EnableState<TStateKey>(TStateKey stateKey) where TStateKey : Enum
        {
            GetActionByState(stateKey).Enable();
        }

        /// <summary>
        /// 根据输入状态语义禁用对应的底层 Action。
        /// </summary>
        public void DisableState<TStateKey>(TStateKey stateKey) where TStateKey : Enum
        {
            GetActionByState(stateKey).Disable();
        }

        /// <summary>
        /// 根据输入状态语义注册对应 Action 的阶段回调。
        /// </summary>
        public void AddStateCallback<TStateKey>(
            TStateKey stateKey,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
            where TStateKey : Enum
        {
            InputAction inputAction = GetActionByState(stateKey);
            BindingCallback(inputAction, phase, callback);
            _stateCallbackRegistrations.Add(new StateCallbackRegistration(inputAction, phase, callback));
        }

        /// <summary>
        /// 根据输入状态语义移除对应 Action 的阶段回调。
        /// </summary>
        public void RemoveStateCallback<TStateKey>(
            TStateKey stateKey,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
            where TStateKey : Enum
        {
            InputAction inputAction = GetActionByState(stateKey);
            UnbindingCallback(inputAction, phase, callback);
            RemovingStateCallbackRegistration(inputAction, phase, callback);
        }

        internal IDisposable RegisterActionCallback(
            InputActionReference actionReference,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
        {
            if (actionReference == null || actionReference.action == null)
            {
                throw new ArgumentNullException(nameof(actionReference));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            InputAction sourceAction = actionReference.action;
            if (sourceAction.actionMap == null || sourceAction.actionMap.id != ActionMap.id)
            {
                throw new ArgumentException("The InputActionReference does not belong to this InputHandle mapping.", nameof(actionReference));
            }

            InputAction action = ActionMap.FindAction(sourceAction.id);
            if (action == null)
            {
                throw new ArgumentException("The InputActionReference could not be resolved in this InputHandle mapping.", nameof(actionReference));
            }

            BindingCallback(action, phase, callback);
            _stateCallbackRegistrations.Add(new StateCallbackRegistration(action, phase, callback));
            return new ActionCallbackRegistration(this, action, phase, callback);
        }

        internal void RemoveActionCallback(InputAction action, InputCallbackPhase phase, Action<InputAction.CallbackContext> callback)
        {
            UnbindingCallback(action, phase, callback);
            RemovingStateCallbackRegistration(action, phase, callback);
        }

        /// <summary>
        /// 根据状态语义查找对应的底层 Action。
        /// </summary>
        protected abstract InputAction GetActionByState<TStateKey>(TStateKey stateKey) where TStateKey : Enum;

        /// <summary>
        /// 根据 Action 名称查找底层 Action。
        /// </summary>
        protected InputAction GetAction(string actionName)
        {
            return ActionMap.FindAction(actionName, throwIfNotFound: true);
        }

        /// <summary>
        /// 清理当前容器注册过的全部回调。
        /// </summary>
        private void ClearingCallbacks()
        {
            for (int i = 0; i < _stateCallbackRegistrations.Count; i++)
            {
                StateCallbackRegistration registration = _stateCallbackRegistrations[i];
                UnbindingCallback(registration.InputAction, registration.Phase, registration.Callback);
            }

            _stateCallbackRegistrations.Clear();
            ClearingContainerCallbacks();
        }

        /// <summary>
        /// 注册指定阶段的输入回调。
        /// </summary>
        private void BindingCallback(
            InputAction inputAction,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
        {
            switch (phase)
            {
                case InputCallbackPhase.Started:
                    inputAction.started += callback;
                    break;
                case InputCallbackPhase.Performed:
                    inputAction.performed += callback;
                    break;
                case InputCallbackPhase.Canceled:
                    inputAction.canceled += callback;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        /// <summary>
        /// 注销指定阶段的输入回调。
        /// </summary>
        private void UnbindingCallback(
            InputAction inputAction,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
        {
            switch (phase)
            {
                case InputCallbackPhase.Started:
                    inputAction.started -= callback;
                    break;
                case InputCallbackPhase.Performed:
                    inputAction.performed -= callback;
                    break;
                case InputCallbackPhase.Canceled:
                    inputAction.canceled -= callback;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        /// <summary>
        /// 移除已记录的输入回调注册。
        /// </summary>
        private void RemovingStateCallbackRegistration(
            InputAction inputAction,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
        {
            for (int i = _stateCallbackRegistrations.Count - 1; i >= 0; i--)
            {
                StateCallbackRegistration registration = _stateCallbackRegistrations[i];
                if (registration.InputAction == inputAction &&
                    registration.Phase == phase &&
                    registration.Callback == callback)
                {
                    _stateCallbackRegistrations.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 清理具体容器注册的完整回调接口。
        /// </summary>
        protected virtual void ClearingContainerCallbacks()
        {
        }

        /// <summary>
        /// 清理当前输入状态。
        /// </summary>
        protected virtual void ClearingState()
        {
        }

        /// <summary>
        /// 刷新输入状态快照。
        /// </summary>
        internal abstract void Tick();

        private readonly struct StateCallbackRegistration
        {
            public readonly InputAction InputAction;
            public readonly InputCallbackPhase Phase;
            public readonly Action<InputAction.CallbackContext> Callback;

            /// <summary>
            /// 创建输入状态回调注册记录。
            /// </summary>
            public StateCallbackRegistration(
                InputAction inputAction,
                InputCallbackPhase phase,
                Action<InputAction.CallbackContext> callback)
            {
                InputAction = inputAction;
                Phase = phase;
                Callback = callback;
            }
        }

        private sealed class ActionCallbackRegistration : IDisposable
        {
            private InputContainer container;
            private readonly InputAction action;
            private readonly InputCallbackPhase phase;
            private readonly Action<InputAction.CallbackContext> callback;

            public ActionCallbackRegistration(InputContainer container, InputAction action, InputCallbackPhase phase, Action<InputAction.CallbackContext> callback)
            {
                this.container = container;
                this.action = action;
                this.phase = phase;
                this.callback = callback;
            }

            public void Dispose()
            {
                if (container == null)
                {
                    return;
                }

                container.RemoveActionCallback(action, phase, callback);
                container = null;
            }
        }
    }

    /// <summary>
    /// 带类型化输入状态的输入容器。
    /// </summary>
    public abstract class InputContainer<TState> : InputContainer where TState : struct
    {
        public TState State { get; protected set; }

        /// <summary>
        /// 清理当前输入状态快照。
        /// </summary>
        protected override void ClearingState()
        {
            State = default;
        }
    }
}
