using UnityEngine;

namespace CGame
{
    [DisallowMultipleComponent]
    public sealed class LevelSpawnPointMarker : MonoBehaviour
    {
        [SerializeField] private SpawnPointKind kind;
        [SerializeField] private string pointId;

        public SpawnPointKind Kind => kind;
        public string PointId => pointId;

        public void Configure(SpawnPointKind pointKind, string id)
        {
            kind = pointKind;
            pointId = id;
        }
    }
}
