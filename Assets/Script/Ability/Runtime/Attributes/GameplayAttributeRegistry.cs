using System;

namespace CGame.Ability.Attributes
{
    public static class GameplayAttributeRegistry
    {
        public static GameplayAttribute Resolve(GameplayAttributeId id)
        {
            switch (id)
            {
                case GameplayAttributeId.CombatBaseDamage:
                    return CombatSet.BaseDamageAttribute;
                case GameplayAttributeId.Health:
                    return HealthSet.HealthAttribute;
                case GameplayAttributeId.MaxHealth:
                    return HealthSet.MaxHealthAttribute;
                case GameplayAttributeId.Damage:
                    return HealthSet.DamageAttribute;
                default:
                    return null;
            }
        }

        public static GameplayAttributeId GetId(GameplayAttribute attribute)
        {
            if (TryGetId(attribute, out GameplayAttributeId id)) return id;
            throw new ArgumentException("The attribute is not registered for serialization.", nameof(attribute));
        }

        public static bool TryGetId(GameplayAttribute attribute, out GameplayAttributeId id)
        {
            if (ReferenceEquals(attribute, CombatSet.BaseDamageAttribute)) id = GameplayAttributeId.CombatBaseDamage;
            else if (ReferenceEquals(attribute, HealthSet.HealthAttribute)) id = GameplayAttributeId.Health;
            else if (ReferenceEquals(attribute, HealthSet.MaxHealthAttribute)) id = GameplayAttributeId.MaxHealth;
            else if (ReferenceEquals(attribute, HealthSet.DamageAttribute)) id = GameplayAttributeId.Damage;
            else id = GameplayAttributeId.None;
            return id != GameplayAttributeId.None;
        }
    }
}
