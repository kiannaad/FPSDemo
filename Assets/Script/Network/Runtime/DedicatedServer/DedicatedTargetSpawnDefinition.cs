using System;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedTargetSpawnDefinition
    {
        public DedicatedTargetSpawnDefinition(GameObject prefab, int count)
        {
            Prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            Count = count == 3
                ? count
                : throw new ArgumentOutOfRangeException(nameof(count), "Dedicated target roster requires exactly three targets.");
        }

        public GameObject Prefab { get; }
        public int Count { get; }
    }
}
