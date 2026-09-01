using UnityEngine;

namespace CGame.Ability.Targeting
{
    public interface IAbilitySystemTargetResolver
    {
        bool TryResolve(Collider collider, out AbilitySystemComponent abilitySystem);
    }
}
