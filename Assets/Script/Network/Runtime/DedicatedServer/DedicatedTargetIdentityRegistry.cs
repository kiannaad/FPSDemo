using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedTargetIdentityRegistry
    {
        private readonly Dictionary<Collider, string> targetIdsByCollider = new Dictionary<Collider, string>();
        private readonly HashSet<string> registeredTargetIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> targetIds = new List<string>();

        public IReadOnlyList<string> TargetIds => targetIds;

        public void Register(string targetId, GameObject targetRoot)
        {
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("Target PointId is required.", nameof(targetId));
            if (targetRoot == null) throw new ArgumentNullException(nameof(targetRoot));
            if (!registeredTargetIds.Add(targetId))
                throw new InvalidOperationException($"Dedicated Target PointId is duplicated: {targetId}.");

            Collider[] colliders = targetRoot.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                registeredTargetIds.Remove(targetId);
                throw new InvalidOperationException($"Dedicated Target has no Collider: {targetId}.");
            }

            try
            {
                for (int index = 0; index < colliders.Length; index++)
                {
                    Collider collider = colliders[index];
                    if (targetIdsByCollider.ContainsKey(collider))
                        throw new InvalidOperationException($"Dedicated Target Collider is already registered: {collider.name}.");
                    targetIdsByCollider.Add(collider, targetId);
                }
                targetIds.Add(targetId);
            }
            catch
            {
                for (int index = 0; index < colliders.Length; index++) targetIdsByCollider.Remove(colliders[index]);
                registeredTargetIds.Remove(targetId);
                targetIds.Remove(targetId);
                throw;
            }
        }

        public string Resolve(Collider collider)
        {
            return collider != null && targetIdsByCollider.TryGetValue(collider, out string targetId)
                ? targetId
                : null;
        }
    }
}
