using System;
using UnityEngine;

namespace CGame
{
    [Serializable]
    public struct SpawnPointSnapshot
    {
        [SerializeField] private string pointId;
        [SerializeField] private SpawnPointKind kind;
        [SerializeField] private int siblingIndex;
        [SerializeField] private Vector3 position;
        [SerializeField] private Quaternion rotation;

        public SpawnPointSnapshot(
            string id,
            SpawnPointKind pointKind,
            int index,
            Vector3 pointPosition,
            Quaternion pointRotation)
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
