using System;
using UnityEngine;

namespace CGame.GameplayTags
{
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private string tagName;

        private GameplayTag(string tagName)
        {
            this.tagName = tagName;
        }

        public static GameplayTag Empty => default;
        public string Name => tagName ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(tagName);

        public static bool TryCreateSerialized(string value, out GameplayTag tag)
        {
            if (!TryNormalizeName(value, out string normalized))
            {
                tag = Empty;
                return false;
            }

            tag = new GameplayTag(normalized);
            return true;
        }

        internal static GameplayTag FromRegisteredName(string value)
        {
            return new GameplayTag(value);
        }

        public bool Equals(GameplayTag other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayTag other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }

        public override string ToString()
        {
            return Name;
        }

        public static bool operator ==(GameplayTag left, GameplayTag right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameplayTag left, GameplayTag right)
        {
            return !left.Equals(right);
        }

        internal static bool TryNormalizeName(string value, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string candidate = value.Trim();
            string[] segments = candidate.Split('.');
            if (segments.Length == 0)
            {
                return false;
            }

            for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                if (!IsValidSegment(segments[segmentIndex]))
                {
                    return false;
                }
            }

            normalized = string.Join(".", segments);
            return true;
        }

        internal static bool IsValidSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
            {
                return false;
            }

            for (int characterIndex = 0; characterIndex < segment.Length; characterIndex++)
            {
                char character = segment[characterIndex];
                bool isAsciiLetter = character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                if (!isAsciiLetter && !isDigit && character != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
