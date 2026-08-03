using System;

namespace CGame.Ability
{
    public readonly struct AbilitySpecHandle : IEquatable<AbilitySpecHandle>
    {
        internal AbilitySpecHandle(int value)
        {
            Value = value;
        }

        internal int Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(AbilitySpecHandle other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilitySpecHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }
}
