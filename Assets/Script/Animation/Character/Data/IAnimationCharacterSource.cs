using UnityEngine;

namespace CGame.Animation
{
    public interface IAnimationCharacterSource
    {
        Transform Transform { get; }
        Vector3 Velocity { get; }
        bool IsGrounded { get; }
    }
}
