using System;

namespace CGame
{
    public readonly struct CharacterRuntimeId : IEquatable<CharacterRuntimeId>
    {
        public CharacterRuntimeId(string value) { Value = value; }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(CharacterRuntimeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CharacterRuntimeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public static bool operator ==(CharacterRuntimeId left, CharacterRuntimeId right) => left.Equals(right);
        public static bool operator !=(CharacterRuntimeId left, CharacterRuntimeId right) => !left.Equals(right);
    }
}
