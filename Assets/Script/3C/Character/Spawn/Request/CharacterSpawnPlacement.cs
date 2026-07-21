using UnityEngine;

namespace CGame
{
    public readonly struct CharacterSpawnPlacement
    {
        public CharacterSpawnPlacement(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool IsValid => !float.IsNaN(Position.x) && !float.IsInfinity(Position.x)
            && !float.IsNaN(Position.y) && !float.IsInfinity(Position.y)
            && !float.IsNaN(Position.z) && !float.IsInfinity(Position.z)
            && !float.IsNaN(Rotation.x) && !float.IsInfinity(Rotation.x)
            && !float.IsNaN(Rotation.y) && !float.IsInfinity(Rotation.y)
            && !float.IsNaN(Rotation.z) && !float.IsInfinity(Rotation.z)
            && !float.IsNaN(Rotation.w) && !float.IsInfinity(Rotation.w);
    }
}
