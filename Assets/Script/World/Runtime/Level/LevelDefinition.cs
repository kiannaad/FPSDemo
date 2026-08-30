using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "CGame/Gameplay/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private string scenePath;
        [SerializeField] private SpawnPointSnapshot[] spawnPoints = Array.Empty<SpawnPointSnapshot>();

        public string ScenePath => scenePath;
        public IReadOnlyList<SpawnPointSnapshot> SpawnPoints => spawnPoints;

        public void SetSnapshot(string path, SpawnPointSnapshot[] points)
        {
            scenePath = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentException("Scene path is required.", nameof(path))
                : path;
            spawnPoints = points == null ? Array.Empty<SpawnPointSnapshot>() : (SpawnPointSnapshot[])points.Clone();
        }
    }

    public enum SpawnPointKind { Player, Enemy }

    [Serializable]
    public struct SpawnPointSnapshot
    {
        [SerializeField] private string pointId;
        [SerializeField] private SpawnPointKind kind;
        [SerializeField] private int siblingIndex;
        [SerializeField] private Vector3 position;
        [SerializeField] private Quaternion rotation;

        public SpawnPointSnapshot(string id, SpawnPointKind pointKind, int index, Vector3 pointPosition, Quaternion pointRotation)
        {
            pointId = id;
            kind = pointKind;
            siblingIndex = index;
            position = pointPosition;
            rotation = pointRotation;
        }

        public string PointId => pointId;
        public SpawnPointKind Kind => kind;
        public int SiblingIndex => siblingIndex;
        public Vector3 Position => position;
        public Quaternion Rotation => rotation;
    }
}
