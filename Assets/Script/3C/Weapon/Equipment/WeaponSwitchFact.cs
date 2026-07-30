namespace CGame
{
    public readonly struct WeaponSwitchFact
    {
        public WeaponSwitchFact(
            ulong switchId,
            WeaponId fromWeaponId,
            WeaponId toWeaponId,
            WeaponSwitchPhase phase,
            WeaponSwitchEndReason endReason =
                WeaponSwitchEndReason.None)
        {
            SwitchId = switchId;
            FromWeaponId = fromWeaponId;
            ToWeaponId = toWeaponId;
            Phase = phase;
            EndReason = endReason;
        }

        public ulong SwitchId { get; }
        public WeaponId FromWeaponId { get; }
        public WeaponId ToWeaponId { get; }
        public WeaponSwitchPhase Phase { get; }
        public WeaponSwitchEndReason EndReason { get; }
        public bool IsValid =>
            SwitchId != 0
            && FromWeaponId.IsValid
            && ToWeaponId.IsValid
            && FromWeaponId != ToWeaponId
            && Phase != WeaponSwitchPhase.None;

        public WeaponSwitchFact End(
            WeaponSwitchPhase phase,
            WeaponSwitchEndReason reason)
        {
            return new WeaponSwitchFact(
                SwitchId,
                FromWeaponId,
                ToWeaponId,
                phase,
                reason);
        }
    }
}
