namespace CGame.Animation
{
    public readonly struct WeaponAnimationDefinitionResolveResult
    {
        public WeaponAnimationDefinitionResolveResult(
            ResolvedWeaponAnimationDefinitionLease lease,
            WeaponAnimationDefinitionResolveError error)
        {
            Lease = lease;
            Error = error;
        }

        public WeaponAnimationDefinitionResolveResult(
            WeaponAnimationDefinition definition,
            WeaponAnimationDefinitionResolveError error)
            : this(
                definition == null
                    ? null
                    : new ResolvedWeaponAnimationDefinitionLease(definition),
                error)
        {
        }

        public ResolvedWeaponAnimationDefinitionLease Lease { get; }
        public WeaponAnimationDefinition Definition => Lease?.Definition;
        public WeaponAnimationDefinitionResolveError Error { get; }
        public bool IsSuccess =>
            Error == WeaponAnimationDefinitionResolveError.None
            && Lease != null
            && !Lease.IsReleased
            && Definition != null;
    }
}
