using System;

namespace CGame.Ability.Cues
{
    public readonly struct GameplayCueHandle : IEquatable<GameplayCueHandle>
    {
        public GameplayCueHandle(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(GameplayCueHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayCueHandle other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(GameplayCueHandle left, GameplayCueHandle right) => left.Equals(right);
        public static bool operator !=(GameplayCueHandle left, GameplayCueHandle right) => !left.Equals(right);
    }
}
