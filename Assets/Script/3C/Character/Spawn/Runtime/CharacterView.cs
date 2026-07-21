using UnityEngine;

namespace CGame
{
    internal sealed class CharacterView : ICharacterView
    {
        private Transform transform;

        public CharacterView(CharacterRuntimeId runtimeId, Transform transform)
        {
            RuntimeId = runtimeId;
            this.transform = transform;
            State = CharacterLifecycleState.CharacterReady;
        }

        public CharacterRuntimeId RuntimeId { get; }
        public Transform Transform => IsValid ? transform : null;
        public CharacterLifecycleState State { get; private set; }
        public bool IsValid => State == CharacterLifecycleState.CharacterReady && transform != null;

        public void Release()
        {
            transform = null;
            State = CharacterLifecycleState.Released;
        }
    }
}
