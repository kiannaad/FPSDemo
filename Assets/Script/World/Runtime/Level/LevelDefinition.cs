using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CGame
{
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "CGame/Gameplay/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private string scenePath;
        [SerializeField] private NavMeshData sceneNavigationData;
        [SerializeField] private SpawnPointSnapshot[] spawnPoints = Array.Empty<SpawnPointSnapshot>();

        public string ScenePath => scenePath;
        // Authored scene-owned data: scene loading/unloading owns its NavMesh instance.
        public NavMeshData SceneNavigationData => sceneNavigationData;
        public IReadOnlyList<SpawnPointSnapshot> SpawnPoints => spawnPoints;

        public void SetSnapshot(string path, SpawnPointSnapshot[] points)
        {
            scenePath = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentException("Scene path is required.", nameof(path))
                : path;
            spawnPoints = points == null ? Array.Empty<SpawnPointSnapshot>() : (SpawnPointSnapshot[])points.Clone();
        }
    }

}
