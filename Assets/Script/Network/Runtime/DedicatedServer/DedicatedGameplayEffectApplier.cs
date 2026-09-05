using System;

namespace CGame.Network
{
    /// <summary>
    /// Dedicated Server's minimal authoritative GameplayEffect boundary.  Combat
    /// resolvers may publish actions, but only an Effect applied here can mutate
    /// an owner Pawn's vitals.
    /// </summary>
    public static class DedicatedGameplayEffectApplier
    {
        public static DedicatedPawnVitalsResult ApplyEnemyDamageEffect(
            DedicatedPawnCombatState target,
            long sourceEnemyId,
            long actionSequence,
            int damage)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return target.ApplyEnemyDamage(sourceEnemyId, actionSequence, damage);
        }
    }
}
