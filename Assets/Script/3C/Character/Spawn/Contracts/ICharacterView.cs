using UnityEngine;

namespace CGame
{
    public interface ICharacterView
    {
        CharacterRuntimeId RuntimeId { get; }
        Transform Transform { get; }
        CharacterLifecycleState State { get; }
        bool IsValid { get; }
    }
}
