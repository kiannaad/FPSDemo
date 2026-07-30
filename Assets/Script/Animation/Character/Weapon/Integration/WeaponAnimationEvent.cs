namespace CGame.Animation
{
    public readonly struct WeaponAnimationEvent
    {
        public WeaponAnimationEvent(WeaponActionFact action)
        {
            Kind = WeaponAnimationEventKind.Action;
            Action = action;
            Switch = default;
        }

        public WeaponAnimationEvent(WeaponSwitchFact weaponSwitch)
        {
            Kind = WeaponAnimationEventKind.Switch;
            Action = default;
            Switch = weaponSwitch;
        }

        public WeaponAnimationEventKind Kind { get; }
        public WeaponActionFact Action { get; }
        public WeaponSwitchFact Switch { get; }
    }
}
