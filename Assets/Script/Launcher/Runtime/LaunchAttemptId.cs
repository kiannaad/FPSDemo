using System;

namespace CGame
{
    public readonly struct LaunchAttemptId : IEquatable<LaunchAttemptId>
    {
        public LaunchAttemptId(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public long Value { get; }

        public bool Equals(LaunchAttemptId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is LaunchAttemptId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();
    }
}
