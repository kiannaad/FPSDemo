using UnityEngine;

namespace CGame
{
    public interface IPlayerCameraRuntime
    {
        Vector3 Position { get; }

        Quaternion Rotation { get; }

        void UpdatePresentation(Pawn pawn, Quaternion controlRotation);
    }
}
