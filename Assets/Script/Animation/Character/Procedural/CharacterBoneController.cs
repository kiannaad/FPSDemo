using System;
using UnityEngine;

namespace CGame.Animation
{
    public sealed class CharacterBoneController : IDisposable
    {
        private readonly Animator animator;
        private bool isDisposed;

        public CharacterBoneController(Animator animator)
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
        }

        public bool IsValid()
        {
            return !isDisposed && animator != null;
        }

        public void Update(float deltaTime)
        {
        }

        public void Dispose()
        {
            isDisposed = true;
        }
    }
}
