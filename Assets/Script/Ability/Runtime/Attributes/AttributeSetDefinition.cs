using System;
using UnityEngine;

namespace CGame.Ability.Attributes
{
    [CreateAssetMenu(menuName = "CGame/Ability/Attribute Set Definition")]
    public sealed class AttributeSetDefinition : ScriptableObject
    {
        [SerializeField] private AttributeSetKind setKind;

        public AttributeSetKind SetKind => setKind;

        public AttributeSet CreateSet()
        {
            switch (setKind)
            {
                case AttributeSetKind.Health:
                    return new HealthSet();
                case AttributeSetKind.Combat:
                    return new CombatSet();
                default:
                    throw new InvalidOperationException($"Unsupported AttributeSet kind: {setKind}.");
            }
        }

        public void ConfigureForTests(AttributeSetKind kind)
        {
            Configure(kind);
        }

        public void Configure(AttributeSetKind kind)
        {
            setKind = kind;
        }
    }
}
