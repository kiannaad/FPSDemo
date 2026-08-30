using System;
using System.Collections;
using System.Collections.Generic;

namespace CGame.Ability.Targeting
{
    public sealed class GameplayAbilityTargetDataHandle : IReadOnlyList<SingleTargetHitData>
    {
        private readonly SingleTargetHitData[] entries;

        public GameplayAbilityTargetDataHandle(IEnumerable<SingleTargetHitData> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            this.entries = new List<SingleTargetHitData>(entries).ToArray();
            if (Array.Exists(this.entries, entry => entry == null))
            {
                throw new ArgumentException("Target data entries cannot contain null.", nameof(entries));
            }
        }

        public static GameplayAbilityTargetDataHandle Empty { get; } =
            new GameplayAbilityTargetDataHandle(Array.Empty<SingleTargetHitData>());

        public int Count => entries.Length;
        public SingleTargetHitData this[int index] => entries[index];

        public IEnumerator<SingleTargetHitData> GetEnumerator()
        {
            return ((IEnumerable<SingleTargetHitData>)entries).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return entries.GetEnumerator();
        }
    }
}
