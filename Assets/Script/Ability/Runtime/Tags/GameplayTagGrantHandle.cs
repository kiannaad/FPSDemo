using System;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public readonly struct GameplayTagGrantHandle : IEquatable<GameplayTagGrantHandle>
    {
        internal GameplayTagGrantHandle(int value, GameplayTag tag)
        {
            Value = value;
            Tag = tag;
        }

        internal int Value { get; }
        public GameplayTag Tag { get; }
        public bool IsValid => Value > 0;

        public bool Equals(GameplayTagGrantHandle other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayTagGrantHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }
}
