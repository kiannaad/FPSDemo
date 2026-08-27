using System;

namespace CGame
{
    public readonly struct ItemInstanceHandle : IEquatable<ItemInstanceHandle>
    {
        public ItemInstanceHandle(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public long Value { get; }

        public bool IsValid => Value > 0;

        public bool Equals(ItemInstanceHandle other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ItemInstanceHandle other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(ItemInstanceHandle left, ItemInstanceHandle right) => left.Equals(right);

        public static bool operator !=(ItemInstanceHandle left, ItemInstanceHandle right) => !left.Equals(right);
    }
}
