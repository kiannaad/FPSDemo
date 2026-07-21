using UnityEngine;

namespace CGame
{
    public class Controller : IController
    {
        private Pawn controlledPawn;
        private Vector2 rotationInput;
        private readonly GameplayRecoilState gameplayRecoilState = new GameplayRecoilState();
        private readonly WeaponRuntime weaponRuntime = new WeaponRuntime();

        public Pawn ControlledPawn => controlledPawn;
        public float ControlYaw { get; private set; }
        public float ControlPitch { get; private set; }
        public float MinPitch { get; private set; } = -89f;
        public float MaxPitch { get; private set; } = 89f;
        public Quaternion ControlRotation { get; private set; } = Quaternion.identity;
        public WeaponRuntime WeaponRuntime => weaponRuntime;

        public bool RequestEquipWeapon(WeaponId weaponId)
        {
            return weaponRuntime.RequestEquip(weaponId);
        }

        public bool RequestUnequipWeapon()
        {
            return weaponRuntime.RequestUnequip();
        }

        public bool RequestFireWeapon(out WeaponActionFact action)
        {
            return weaponRuntime.RequestFire(out action);
        }

        public bool CompleteWeaponAction(ulong actionId)
        {
            return weaponRuntime.CompleteAction(actionId);
        }

        public bool CancelWeaponAction(ulong actionId, WeaponActionEndReason reason = WeaponActionEndReason.Cancelled)
        {
            return weaponRuntime.CancelAction(actionId, reason);
        }

        /// <summary>
        /// 更新控制器逻辑，并把控制旋转同步给当前 Pawn。
        /// </summary>
        public virtual void UpdatingController(float elapseSeconds)
        {
            UpdatingControlRotation();
            ApplyRotationDelta(gameplayRecoilState.Advance(elapseSeconds));
            ApplyingControlRotationToPawn();
        }

        public void ApplyingGameplayRecoil(Vector2 kick, float recoveryDegreesPerSecond)
        {
            Vector2 appliedKick = ApplyRotationDelta(kick);
            gameplayRecoilState.ApplyKick(appliedKick, recoveryDegreesPerSecond);
        }

        public void ClearingGameplayRecoil()
        {
            ApplyRotationDelta(gameplayRecoilState.Clear());
        }

        /// <summary>
        /// 控制指定 Pawn。
        /// </summary>
        public virtual void PossessingPawn(Pawn pawn)
        {
            if (controlledPawn == pawn)
            {
                return;
            }

            UnpossessingPawn();
            controlledPawn = pawn;
            controlledPawn?.SettingController(this);
        }

        /// <summary>
        /// 解除当前 Pawn 的控制关系。
        /// </summary>
        public virtual void UnpossessingPawn()
        {
            if (controlledPawn == null)
            {
                return;
            }

            Pawn oldPawn = controlledPawn;
            controlledPawn = null;
            oldPawn.ClearingController(this);
            oldPawn.ClearingControlIntent();
        }

        /// <summary>
        /// 设置控制旋转。
        /// </summary>
        public void SettingControlRotation(Quaternion controlRotation)
        {
            Vector3 eulerAngles = controlRotation.eulerAngles;
            ControlYaw = eulerAngles.y;
            ControlPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, eulerAngles.x), MinPitch, MaxPitch);
            SynchronizeControlRotation();
        }

        /// <summary>
        /// 设置控制俯仰范围，并立即约束当前 Pitch。
        /// </summary>
        public void SettingPitchLimits(float minPitch, float maxPitch)
        {
            MinPitch = Mathf.Min(minPitch, maxPitch);
            MaxPitch = Mathf.Max(minPitch, maxPitch);
            ControlPitch = Mathf.Clamp(ControlPitch, MinPitch, MaxPitch);
            SynchronizeControlRotation();
        }

        /// <summary>
        /// 累加俯仰旋转输入。
        /// </summary>
        public void AddingPitchInput(float value)
        {
            rotationInput.x += value;
        }

        /// <summary>
        /// 累加偏航旋转输入。
        /// </summary>
        public void AddingYawInput(float value)
        {
            rotationInput.y += value;
        }

        /// <summary>
        /// 按 UE 风格消费本帧旋转输入并更新控制旋转。
        /// </summary>
        protected virtual void UpdatingControlRotation()
        {
            if (rotationInput == Vector2.zero)
            {
                return;
            }

            ControlPitch = Mathf.Clamp(ControlPitch + rotationInput.x, MinPitch, MaxPitch);
            ControlYaw += rotationInput.y;
            rotationInput = Vector2.zero;
            SynchronizeControlRotation();
        }

        /// <summary>
        /// 把控制旋转应用到当前 Pawn。
        /// </summary>
        protected virtual void ApplyingControlRotationToPawn()
        {
            controlledPawn?.ApplyingControlRotation(ControlRotation);
        }

        private void SynchronizeControlRotation()
        {
            ControlRotation = Quaternion.Euler(ControlPitch, ControlYaw, 0f);
        }

        private Vector2 ApplyRotationDelta(Vector2 delta)
        {
            if (delta == Vector2.zero)
            {
                return Vector2.zero;
            }

            float previousPitch = ControlPitch;
            float previousYaw = ControlYaw;
            ControlPitch = Mathf.Clamp(ControlPitch + delta.x, MinPitch, MaxPitch);
            ControlYaw += delta.y;
            SynchronizeControlRotation();
            return new Vector2(ControlPitch - previousPitch, ControlYaw - previousYaw);
        }
    }
}
