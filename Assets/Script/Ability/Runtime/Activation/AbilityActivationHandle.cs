using System;

namespace CGame.Ability
{
    public readonly struct AbilityActivationHandle : IEquatable<AbilityActivationHandle>
    {
        internal AbilityActivationHandle(long value)
        {
            Value = value;
        }

        internal long Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(AbilityActivationHandle other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityActivationHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
