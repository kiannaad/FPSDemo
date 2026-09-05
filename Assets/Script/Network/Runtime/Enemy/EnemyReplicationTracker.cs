using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public sealed class EnemyReplicationTracker
    {
        private const float UnknownEntityResyncDelaySeconds = 2f;
        private readonly Dictionary<long, EnemyReplicationState> statesByEnemyId = new Dictionary<long, EnemyReplicationState>();
        private readonly Dictionary<long, UnknownEnemyState> unknownByEnemyId = new Dictionary<long, UnknownEnemyState>();

        public int Count => statesByEnemyId.Count;

        public bool ApplySpawn(EnemySpawnedEvent spawned)
        {
            if (spawned == null || spawned.EnemyId <= 0 || string.IsNullOrWhiteSpace(spawned.ArchetypeId) ||
                spawned.AuthorityServerTick <= 0)
                return false;
            if (statesByEnemyId.ContainsKey(spawned.EnemyId)) return false;

            var state = new EnemyReplicationState(spawned);
            statesByEnemyId.Add(spawned.EnemyId, state);
            if (!unknownByEnemyId.TryGetValue(spawned.EnemyId, out UnknownEnemyState unknown)) return true;

            unknownByEnemyId.Remove(spawned.EnemyId);
            if (unknown.Snapshot != null) state.TryAcceptSnapshot(unknown.Snapshot);
            if (unknown.Action != null) state.TryAcceptAction(unknown.Action);
            return true;
        }

        public bool ApplySnapshot(EnemySnapshotEvent snapshot, float observedAtSeconds = 0f)
        {
            if (!IsValid(snapshot)) return false;
            if (statesByEnemyId.TryGetValue(snapshot.EnemyId, out EnemyReplicationState state))
                return state.TryAcceptSnapshot(snapshot);

            RecordUnknownSnapshot(snapshot, observedAtSeconds);
            return false;
        }

        public bool ApplyAction(EnemyActionEvent action, float observedAtSeconds = 0f)
        {
            if (action == null || action.EnemyId <= 0 || action.ActionSequence <= 0 || action.AuthorityServerTick <= 0)
                return false;
            if (statesByEnemyId.TryGetValue(action.EnemyId, out EnemyReplicationState state))
                return state.TryAcceptAction(action);

            RecordUnknownAction(action, observedAtSeconds);
            return false;
        }

        public bool TryGet(long enemyId, out EnemyReplicationState state) => statesByEnemyId.TryGetValue(enemyId, out state);

        public IReadOnlyList<EnemyResyncRequest> CollectExpiredResyncRequests(float nowSeconds)
        {
            var requests = new List<EnemyResyncRequest>();
            foreach (KeyValuePair<long, UnknownEnemyState> pair in unknownByEnemyId)
            {
                UnknownEnemyState unknown = pair.Value;
                if (unknown.ResyncRequested || nowSeconds - unknown.FirstObservedAtSeconds < UnknownEntityResyncDelaySeconds)
                    continue;

                unknown.ResyncRequested = true;
                requests.Add(new EnemyResyncRequest
                {
                    EnemyId = pair.Key,
                    LastKnownAuthorityServerTick = unknown.Snapshot?.AuthorityServerTick ?? unknown.Action?.AuthorityServerTick ?? 0
                });
            }
            return requests;
        }

        public void Clear()
        {
            statesByEnemyId.Clear();
            unknownByEnemyId.Clear();
        }

        private static bool IsValid(EnemySnapshotEvent snapshot) =>
            snapshot != null && snapshot.EnemyId > 0 && snapshot.AuthorityServerTick > 0;

        private void RecordUnknownSnapshot(EnemySnapshotEvent snapshot, float observedAtSeconds)
        {
            UnknownEnemyState unknown = GetOrCreateUnknown(snapshot.EnemyId, observedAtSeconds);
            if (unknown.Snapshot == null || snapshot.AuthorityServerTick > unknown.Snapshot.AuthorityServerTick)
                unknown.Snapshot = snapshot;
        }

        private void RecordUnknownAction(EnemyActionEvent action, float observedAtSeconds)
        {
            UnknownEnemyState unknown = GetOrCreateUnknown(action.EnemyId, observedAtSeconds);
            if (unknown.Action == null || action.ActionSequence > unknown.Action.ActionSequence)
                unknown.Action = action;
        }

        private UnknownEnemyState GetOrCreateUnknown(long enemyId, float observedAtSeconds)
        {
            if (unknownByEnemyId.TryGetValue(enemyId, out UnknownEnemyState current)) return current;
            current = new UnknownEnemyState(observedAtSeconds);
            unknownByEnemyId.Add(enemyId, current);
            return current;
        }

        private sealed class UnknownEnemyState
        {
            public UnknownEnemyState(float firstObservedAtSeconds) { FirstObservedAtSeconds = firstObservedAtSeconds; }
            public float FirstObservedAtSeconds { get; }
            public EnemySnapshotEvent Snapshot { get; set; }
            public EnemyActionEvent Action { get; set; }
            public bool ResyncRequested { get; set; }
        }
    }

    public sealed class EnemyReplicationState
    {
        public EnemyReplicationState(EnemySpawnedEvent spawned)
        {
            Spawn = spawned;
            LatestAuthorityServerTick = spawned.AuthorityServerTick;
        }

        public EnemySpawnedEvent Spawn { get; }
        public long LatestAuthorityServerTick { get; private set; }
        public long LatestActionSequence { get; private set; }
        public EnemySnapshotEvent LatestSnapshot { get; private set; }
        public EnemyActionEvent LatestAction { get; private set; }

        internal bool TryAcceptSnapshot(EnemySnapshotEvent snapshot)
        {
            if (snapshot.AuthorityServerTick <= LatestAuthorityServerTick) return false;
            LatestAuthorityServerTick = snapshot.AuthorityServerTick;
            LatestSnapshot = snapshot;
            return true;
        }

        internal bool TryAcceptAction(EnemyActionEvent action)
        {
            if (action.ActionSequence <= LatestActionSequence) return false;
            LatestActionSequence = action.ActionSequence;
            LatestAction = action;
            return true;
        }
    }
}
