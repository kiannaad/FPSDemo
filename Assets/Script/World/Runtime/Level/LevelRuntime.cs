using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame
{
    public sealed class LevelRuntime
    {
        public const string OccupiedTagName = "State.SpawnPoint.Occupied";
        private readonly Dictionary<string, PointRecord> points = new Dictionary<string, PointRecord>(StringComparer.Ordinal);
        private bool shutdown;

        public LevelRuntime(LevelDefinition definition, Scene scene)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!scene.IsValid() || !scene.isLoaded || scene.path != definition.ScenePath)
                throw new InvalidOperationException(
                    $"LevelDefinition Scene snapshot does not match the active Scene. " +
                    $"SceneValid={scene.IsValid()} SceneLoaded={scene.isLoaded} ScenePath={scene.path} DefinitionPath={definition.ScenePath}");
            BuildAndValidate(definition, scene);
        }

        public IReadOnlyCollection<string> PointIds => points.Keys;

        public SpawnPointReservation ReserveFirstPlayerPoint()
        {
            foreach (PointRecord point in points.Values)
                if (point.Kind == SpawnPointKind.Player && point.State == SpawnPointState.Available) return Reserve(point);
            throw new InvalidOperationException("No available PlayerPoint exists.");
        }

        public IReadOnlyList<SpawnPointReservation> ReserveRandomEnemyPoints(int count, System.Random random)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var available = new List<PointRecord>();
            foreach (PointRecord point in points.Values)
                if (point.Kind == SpawnPointKind.Enemy && point.State == SpawnPointState.Available) available.Add(point);
            if (available.Count < count) throw new InvalidOperationException($"Requested {count} EnemyPoints but only {available.Count} are available.");
            var reservations = new List<SpawnPointReservation>(count);
            for (int index = 0; index < count; index++)
            {
                int selected = random.Next(available.Count);
                reservations.Add(Reserve(available[selected]));
                available.RemoveAt(selected);
            }
            return reservations;
        }

        public bool ReleaseOccupied(string pointId, Guid registrationId)
        {
            if (!points.TryGetValue(pointId, out PointRecord point) || point.State != SpawnPointState.Occupied || point.RegistrationId != registrationId) return false;
            point.State = SpawnPointState.Available;
            point.RegistrationId = Guid.Empty;
            return true;
        }

        public SpawnPointStatus GetStatus(string pointId)
        {
            if (!points.TryGetValue(pointId, out PointRecord point)) throw new KeyNotFoundException(pointId);
            return new SpawnPointStatus(point.Id, point.Kind, point.Transform, point.State, point.RegistrationId);
        }

        public void Shutdown()
        {
            foreach (PointRecord point in points.Values) { point.State = SpawnPointState.Available; point.RegistrationId = Guid.Empty; }
            shutdown = true;
        }

        private SpawnPointReservation Reserve(PointRecord point)
        {
            if (shutdown) throw new ObjectDisposedException(nameof(LevelRuntime));
            point.State = SpawnPointState.Reserved;
            return new SpawnPointReservation(this, point.Id, point.Kind, point.Transform);
        }

        internal void Commit(string pointId, Guid registrationId)
        {
            if (registrationId == Guid.Empty) throw new ArgumentException("RegistrationId is required.", nameof(registrationId));
            PointRecord point = points[pointId];
            if (point.State != SpawnPointState.Reserved) throw new InvalidOperationException($"Point {pointId} is not Reserved.");
            point.State = SpawnPointState.Occupied;
            point.RegistrationId = registrationId;
        }

        internal void Rollback(string pointId)
        {
            PointRecord point = points[pointId];
            if (point.State == SpawnPointState.Reserved) point.State = SpawnPointState.Available;
        }

        private void BuildAndValidate(LevelDefinition definition, Scene scene)
        {
            var markers = new List<LevelSpawnPointMarker>();
            foreach (GameObject root in scene.GetRootGameObjects()) markers.AddRange(root.GetComponentsInChildren<LevelSpawnPointMarker>(true));
            if (markers.Count != definition.SpawnPoints.Count) throw new InvalidOperationException("Level spawn snapshot count is stale.");
            for (int index = 0; index < definition.SpawnPoints.Count; index++)
            {
                SpawnPointSnapshot snapshot = definition.SpawnPoints[index];
                LevelSpawnPointMarker marker = markers.Find(candidate => candidate.PointId == snapshot.PointId && candidate.Kind == snapshot.Kind);
                if (marker == null || marker.transform.GetSiblingIndex() != snapshot.SiblingIndex || marker.transform.position != snapshot.Position || Quaternion.Angle(marker.transform.rotation, snapshot.Rotation) > 0.001f)
                    throw new InvalidOperationException($"Level spawn snapshot is stale at {snapshot.PointId}.");
                if (points.ContainsKey(snapshot.PointId)) throw new InvalidOperationException($"Duplicate PointId {snapshot.PointId}.");
                points.Add(snapshot.PointId, new PointRecord(snapshot.PointId, snapshot.Kind, marker.transform));
            }
        }

        private sealed class PointRecord
        {
            public PointRecord(string id, SpawnPointKind kind, Transform transform) { Id = id; Kind = kind; Transform = transform; }
            public string Id { get; }
            public SpawnPointKind Kind { get; }
            public Transform Transform { get; }
            public SpawnPointState State { get; set; }
            public Guid RegistrationId { get; set; }
        }
    }

    public enum SpawnPointState { Available, Reserved, Occupied }

    public readonly struct SpawnPointStatus
    {
        public SpawnPointStatus(string id, SpawnPointKind kind, Transform transform, SpawnPointState state, Guid registrationId) { PointId = id; Kind = kind; Transform = transform; State = state; RegistrationId = registrationId; }
        public string PointId { get; }
        public SpawnPointKind Kind { get; }
        public Transform Transform { get; }
        public SpawnPointState State { get; }
        public Guid RegistrationId { get; }
        public bool HasOccupiedTag => State == SpawnPointState.Occupied;
    }

    public sealed class SpawnPointReservation : IDisposable
    {
        private LevelRuntime owner;
        internal SpawnPointReservation(LevelRuntime runtime, string pointId, SpawnPointKind kind, Transform transform) { owner = runtime; PointId = pointId; Kind = kind; Transform = transform; }
        public string PointId { get; }
        public SpawnPointKind Kind { get; }
        public Transform Transform { get; }
        public void Commit(Guid registrationId) { if (owner == null) throw new ObjectDisposedException(nameof(SpawnPointReservation)); owner.Commit(PointId, registrationId); owner = null; }
        public void Dispose() { owner?.Rollback(PointId); owner = null; }
    }
}
