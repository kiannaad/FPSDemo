using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public sealed class EnemyPresentationRegistry : IDisposable
    {
        private const float InterpolationSeconds = 0.1f;
        private const float StreamFreezeSeconds = 0.5f;
        private const float DeathTimeoutSeconds = 4f;
        private readonly EnemyPresentationCatalog catalog;
        private readonly IEnemyActionCueSink cueSink;
        private readonly Dictionary<long, Entry> entriesByEnemyId = new Dictionary<long, Entry>();
        private readonly HashSet<long> deadEnemyIds = new HashSet<long>();
        private readonly List<long> completedDeaths = new List<long>();

        public EnemyPresentationRegistry(EnemyPresentationCatalog catalog, IEnemyActionCueSink cueSink = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.cueSink = cueSink;
        }

        public int Count => entriesByEnemyId.Count;

        public bool Spawn(EnemySpawnedEvent spawned)
        {
            if (spawned == null || deadEnemyIds.Contains(spawned.EnemyId) || entriesByEnemyId.ContainsKey(spawned.EnemyId) ||
                !catalog.TryGet(spawned.ArchetypeId, out EnemyArchetypeSpec archetype) || archetype.PresentationPrefab == null)
                return false;

            GameObject root = UnityEngine.Object.Instantiate(
                archetype.PresentationPrefab,
                spawned.Position.ToValue().ToMeters(),
                spawned.Rotation.ToValue().ToQuaternion());
            entriesByEnemyId.Add(spawned.EnemyId, new Entry(
                root,
                root.GetComponentInChildren<EnemyPresentation>(true),
                root.transform.position,
                root.transform.rotation));
            return true;
        }

        public bool ApplySnapshot(EnemySnapshotEvent snapshot)
        {
            if (snapshot == null || deadEnemyIds.Contains(snapshot.EnemyId) ||
                !entriesByEnemyId.TryGetValue(snapshot.EnemyId, out Entry entry)) return false;
            entry.TargetPosition = snapshot.Position.ToValue().ToMeters();
            entry.TargetRotation = snapshot.Rotation.ToValue().ToQuaternion();
            entry.LastSnapshotReceivedAtSeconds = Time.realtimeSinceStartup;
            entry.Presentation?.ApplyRemoteAnimationState(
                RemoteEnemyAnimationState.FromSnapshot(snapshot),
                Time.deltaTime);
            return true;
        }

        public void Tick(float deltaTime, float nowSeconds)
        {
            if (deltaTime <= 0f) return;
            float alpha = Mathf.Clamp01(deltaTime / InterpolationSeconds);
            completedDeaths.Clear();
            foreach (KeyValuePair<long, Entry> pair in entriesByEnemyId)
            {
                Entry entry = pair.Value;
                entry.Presentation?.TickPresentation(deltaTime);
                if (deadEnemyIds.Contains(pair.Key))
                {
                    if (entry.Presentation != null && entry.Presentation.IsDeathPresentationComplete ||
                        nowSeconds - entry.DeathStartedAtSeconds >= DeathTimeoutSeconds)
                        completedDeaths.Add(pair.Key);
                    continue;
                }
                if (nowSeconds - entry.LastSnapshotReceivedAtSeconds > StreamFreezeSeconds) continue;
                entry.Root.transform.SetPositionAndRotation(
                    Vector3.Lerp(entry.Root.transform.position, entry.TargetPosition, alpha),
                    Quaternion.Slerp(entry.Root.transform.rotation, entry.TargetRotation, alpha));
            }
            foreach (long enemyId in completedDeaths) Remove(enemyId);
        }

        public bool ApplyAction(EnemyActionEvent action)
        {
            if (action == null || deadEnemyIds.Contains(action.EnemyId) ||
                !entriesByEnemyId.TryGetValue(action.EnemyId, out Entry entry) ||
                action.ActionSequence <= entry.LastActionSequence) return false;
            entry.LastActionSequence = action.ActionSequence;
            cueSink?.TryExecute(action, entry.Root);
            if (action.ActionKind == EnemyActionKind.Fire) entry.Presentation?.PlayFire();
            if (action.ActionKind == EnemyActionKind.Hit) entry.Presentation?.PlayHit();
            if (action.ActionKind == EnemyActionKind.Death)
            {
                deadEnemyIds.Add(action.EnemyId);
                entry.DeathStartedAtSeconds = Time.realtimeSinceStartup;
                entry.Presentation?.PlayDeath();
            }
            return true;
        }

        public void Remove(long enemyId)
        {
            if (!entriesByEnemyId.Remove(enemyId, out Entry entry)) return;
            DestroyRoot(entry.Root);
        }

        public void Dispose()
        {
            foreach (Entry entry in entriesByEnemyId.Values)
                DestroyRoot(entry.Root);
            entriesByEnemyId.Clear();
            deadEnemyIds.Clear();
            completedDeaths.Clear();
        }

        private static void DestroyRoot(GameObject root)
        {
            if (root == null) return;
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }
#endif
            UnityEngine.Object.Destroy(root);
        }

        private sealed class Entry
        {
            public Entry(GameObject root, EnemyPresentation presentation, Vector3 position, Quaternion rotation)
            {
                Root = root;
                Presentation = presentation;
                TargetPosition = position;
                TargetRotation = rotation;
                LastSnapshotReceivedAtSeconds = Time.realtimeSinceStartup;
            }
            public GameObject Root { get; }
            public EnemyPresentation Presentation { get; }
            public Vector3 TargetPosition { get; set; }
            public Quaternion TargetRotation { get; set; }
            public float LastSnapshotReceivedAtSeconds { get; set; }
            public float DeathStartedAtSeconds { get; set; }
            public long LastActionSequence { get; set; }
        }
    }
}
