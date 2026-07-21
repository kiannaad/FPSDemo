using System;

namespace CGame
{
    public readonly struct CharacterSpawnRequestId : IEquatable<CharacterSpawnRequestId>
    {
        public CharacterSpawnRequestId(string value) { Value = value; }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(CharacterSpawnRequestId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CharacterSpawnRequestId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public static bool operator ==(CharacterSpawnRequestId left, CharacterSpawnRequestId right) => left.Equals(right);
        public static bool operator !=(CharacterSpawnRequestId left, CharacterSpawnRequestId right) => !left.Equals(right);
    }
}
