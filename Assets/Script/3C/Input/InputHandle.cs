using System;
using UnityEngine.InputSystem;

namespace CGame
{
    public class InputHandle
    {
        private readonly InputContainer container;

        /// <summary>
        /// 创建输入操作句柄。
        /// </summary>
        internal InputHandle(InputContainer container)
        {
            this.container = container;
        }

        /// <summary>
        /// 启用整个输入方案。
        /// </summary>
        public void Enable() => container.Enable();

        /// <summary>
        /// 禁用整个输入方案。
        /// </summary>
        public void Disable() => container.Disable();

        /// <summary>
        /// 根据输入状态语义启用对应输入。
        /// </summary>
        public void EnableState<TStateKey>(TStateKey stateKey) where TStateKey : Enum
            => container.EnableState(stateKey);

        /// <summary>
        /// 根据输入状态语义禁用对应输入。
        /// </summary>
        public void DisableState<TStateKey>(TStateKey stateKey) where TStateKey : Enum
            => container.DisableState(stateKey);

        /// <summary>
        /// 根据输入状态语义注册对应输入回调。
        /// </summary>
        public void AddStateCallback<TStateKey>(
            TStateKey stateKey,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
            where TStateKey : Enum
            => container.AddStateCallback(stateKey, phase, callback);

        /// <summary>
        /// 根据输入状态语义移除对应输入回调。
        /// </summary>
        public void RemoveStateCallback<TStateKey>(
            TStateKey stateKey,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
            where TStateKey : Enum
            => container.RemoveStateCallback(stateKey, phase, callback);

        /// <summary>
        /// 读取当前输入状态快照。
        /// </summary>
        public TState GetState<TState>() where TState : struct
        {
            if (container is InputContainer<TState> typed)
            {
                return typed.State;
            }

            throw new InvalidOperationException($"Container 类型不匹配，期望 InputContainer<{typeof(TState).Name}>");
        }

        /// <summary>
        /// 注册完整输入回调接口。
        /// </summary>
        public void AddCallbacks<TCallback>(TCallback callbacks)
        {
            if (container is ICallbackContainer<TCallback> cb)
            {
                cb.AddCallbacks(callbacks);
            }
            else
            {
                throw new InvalidOperationException($"Container 不支持回调类型 {typeof(TCallback).Name}");
            }
        }

        /// <summary>
        /// 移除完整输入回调接口。
        /// </summary>
        public void RemoveCallbacks<TCallback>(TCallback callbacks)
        {
            if (container is ICallbackContainer<TCallback> cb)
            {
                cb.RemoveCallbacks(callbacks);
            }
        }

        public IDisposable RegisterActionCallback(
            InputActionReference actionReference,
            InputCallbackPhase phase,
            Action<InputAction.CallbackContext> callback)
        {
            return container.RegisterActionCallback(actionReference, phase, callback);
        }

        /// <summary>
        /// 刷新输入状态快照。
        /// </summary>
        internal void Tick() => container.Tick();
    }
}
