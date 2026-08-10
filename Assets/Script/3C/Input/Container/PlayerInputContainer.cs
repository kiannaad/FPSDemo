using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame
{
    public class PlayerInputContainer : InputContainer<PlayerInputState>,
        ICallbackContainer<PlayerInput.IPlayerActions>
    {
        private static readonly Dictionary<PlayerInputStateKey, PlayerAction> stateToActionMap =
            new Dictionary<PlayerInputStateKey, PlayerAction>
            {
                [PlayerInputStateKey.MoveInput] = PlayerAction.Move,
                [PlayerInputStateKey.LookInput] = PlayerAction.Look,
                [PlayerInputStateKey.FirePressed] = PlayerAction.Fire,
                [PlayerInputStateKey.FireHeld] = PlayerAction.Fire,
                [PlayerInputStateKey.JumpPressed] = PlayerAction.Jump,
                [PlayerInputStateKey.SprintHeld] = PlayerAction.Sprint,
                [PlayerInputStateKey.AimHeld] = PlayerAction.Aim,
                [PlayerInputStateKey.ReloadPressed] = PlayerAction.Reload,
            };

        private readonly PlayerInput input;
        private readonly List<PlayerInput.IPlayerActions> callbacks = new List<PlayerInput.IPlayerActions>();

        /// <summary>
        /// 创建 Player 输入容器。
        /// </summary>
        public PlayerInputContainer(PlayerInput input)
        {
            this.input = input;
        }

        protected override InputActionMap ActionMap => input.Player.Get();

        /// <summary>
        /// 注册 Player 输入回调接口。
        /// </summary>
        public void AddCallbacks(PlayerInput.IPlayerActions cb)
        {
            input.Player.AddCallbacks(cb);
            callbacks.Add(cb);
        }

        /// <summary>
        /// 移除 Player 输入回调接口。
        /// </summary>
        public void RemoveCallbacks(PlayerInput.IPlayerActions cb)
        {
            input.Player.RemoveCallbacks(cb);
            callbacks.Remove(cb);
        }

        /// <summary>
        /// 根据 Player 输入状态语义查找对应的底层 Action。
        /// </summary>
        protected override InputAction GetActionByState<TStateKey>(TStateKey stateKey)
        {
            if (stateKey is not PlayerInputStateKey playerStateKey)
            {
                throw new InvalidOperationException($"PlayerInputContainer 不支持状态类型 {typeof(TStateKey).Name}");
            }

            if (!stateToActionMap.TryGetValue(playerStateKey, out PlayerAction action))
            {
                throw new InvalidOperationException($"PlayerInputContainer 未配置状态映射 {playerStateKey}");
            }

            return GetAction(action.ToString());
        }

        /// <summary>
        /// 刷新 Player 输入状态快照。
        /// </summary>
        internal override void Tick()
        {
            State = new PlayerInputState
            {
                MoveInput = readMoveInput(),
                LookInput = ReadLookInput(),
                FirePressed = input.Player.Fire.WasPressedThisFrame()
                    || Mouse.current?.leftButton.wasPressedThisFrame == true,
                FireHeld = input.Player.Fire.IsPressed()
                    || Mouse.current?.leftButton.isPressed == true,
                JumpPressed = input.Player.Jump.WasPressedThisFrame()
                    || Keyboard.current?.spaceKey.wasPressedThisFrame == true,
                SprintHeld = input.Player.Sprint.IsPressed()
                    || Keyboard.current?.leftShiftKey.isPressed == true,
                AimHeld = input.Player.Aim.IsPressed(),
                ReloadPressed = input.Player.Reload.WasPressedThisFrame()
                    || Keyboard.current?.rKey.wasPressedThisFrame == true,
            };
        }

        private Vector2 readMoveInput()
        {
            Vector2 actionValue = input.Player.Move.ReadValue<Vector2>();
            Keyboard keyboard = Keyboard.current;
            if (actionValue.sqrMagnitude > 0f || keyboard == null)
            {
                return Vector2.ClampMagnitude(actionValue, 1f);
            }

            float horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float vertical = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        /// <summary>
        /// 清理 Player 输入方案注册的完整回调接口。
        /// </summary>
        protected override void ClearingContainerCallbacks()
        {
            for (int i = 0; i < callbacks.Count; i++)
            {
                input.Player.RemoveCallbacks(callbacks[i]);
            }

            callbacks.Clear();
        }

        private LookInputValue ReadLookInput()
        {
            InputAction lookAction = input.Player.Look;
            Vector2 value = lookAction.ReadValue<Vector2>();

            if (value.sqrMagnitude <= 0f && Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                if (mouseDelta.sqrMagnitude > 0f)
                {
                    return new LookInputValue(mouseDelta, LookInputTimeMode.Delta);
                }
            }

            if (lookAction.activeControl == null)
            {
                return new LookInputValue(Vector2.zero, LookInputTimeMode.None);
            }

            LookInputTimeMode timeMode = lookAction.activeControl.device is Pointer
                ? LookInputTimeMode.Delta
                : LookInputTimeMode.Rate;
            return new LookInputValue(value, timeMode);
        }
    }
}
