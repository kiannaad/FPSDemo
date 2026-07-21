using System;
using UnityEngine;

namespace CGame
{
    public class PlayerController : Controller
    {
        private Func<PlayerInputState> inputStateProvider;
        private ICombatIntentSink combatIntentSink;

        public float MouseSensitivity { get; set; } = 1f;
        public float StickDegreesPerSecond { get; set; } = 180f;
        public float LookSensitivityMultiplier { get; private set; } = 1f;
        public bool AimHeld { get; private set; }

        public void SettingInputStateProvider(Func<PlayerInputState> provider)
        {
            inputStateProvider = provider;
            if (provider == null)
            {
                AimHeld = false;
            }
        }

        public void SettingInputHandle(InputHandle handle)
        {
            inputStateProvider = handle == null
                ? null
                : () => handle.GetState<PlayerInputState>();
            if (handle == null)
            {
                AimHeld = false;
            }
        }

        public void SettingLookSensitivityMultiplier(float multiplier)
        {
            if (multiplier < 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            }

            LookSensitivityMultiplier = multiplier;
        }

        public void SettingCombatIntentSink(ICombatIntentSink sink)
        {
            if (combatIntentSink == sink)
            {
                return;
            }

            combatIntentSink?.SubmitCombatIntent(default);
            combatIntentSink = sink;
        }

        public override void UnpossessingPawn()
        {
            combatIntentSink?.SubmitCombatIntent(default);
            base.UnpossessingPawn();
        }

        /// <summary>
        /// 更新玩家控制器逻辑。
        /// </summary>
        public override void UpdatingController(float elapseSeconds)
        {
            bool hasInputState = inputStateProvider != null;
            PlayerInputState inputState = hasInputState ? inputStateProvider() : default;
            AimHeld = hasInputState && inputState.AimHeld;
            if (hasInputState && inputState.LookInput.TimeMode != LookInputTimeMode.None)
            {
                float sensitivity = inputState.LookInput.TimeMode == LookInputTimeMode.Delta
                    ? MouseSensitivity
                    : StickDegreesPerSecond;
                Vector2 lookDelta = inputState.LookInput.ResolveFrameDelta(elapseSeconds) *
                    sensitivity * LookSensitivityMultiplier;
                AddingPitchInput(-lookDelta.y);
                AddingYawInput(lookDelta.x);
            }

            base.UpdatingController(elapseSeconds);

            if (ControlledPawn == null || !hasInputState)
            {
                return;
            }

            Vector3 localDirection = new Vector3(inputState.MoveInput.x, 0f, inputState.MoveInput.y);
            Quaternion movementRotation = Quaternion.Euler(0f, ControlYaw, 0f);
            Vector3 worldDirection = movementRotation * localDirection;
            ControlledPawn.SubmitControlIntent(
                new CharacterControlIntent(worldDirection, inputState.JumpPressed));
            combatIntentSink?.SubmitCombatIntent(new CharacterCombatIntent(
                ControlRotation * Vector3.forward,
                inputState.FirePressed || inputState.FireHeld,
                inputState.ReloadPressed));
        }
    }
}
