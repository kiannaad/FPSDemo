using System;
using UnityEngine;

namespace CGame
{
    public sealed class CharacterAssembly : IDisposable
    {
        private bool ownsAssembly = true;

        internal CharacterAssembly(
            GameObject root,
            Animator animator,
            Character character,
            PawnHost pawnHost,
            CharacterPhysicsMotor motor,
            MovementComp movement,
            CharacterAnimationComponent animationComponent)
        {
            Root = root;
            Animator = animator;
            Character = character;
            PawnHost = pawnHost;
            Motor = motor;
            Movement = movement;
            AnimationComponent = animationComponent;
        }

        public GameObject Root { get; }
        public Animator Animator { get; }
        public Character Character { get; }
        public PawnHost PawnHost { get; }
        public CharacterPhysicsMotor Motor { get; }
        public MovementComp Movement { get; }
        public CharacterAnimationComponent AnimationComponent { get; }
        public bool IsOwnershipTransferred => !ownsAssembly;

        public void TransferRuntimeOwnership()
        {
            if (Root == null)
            {
                throw new InvalidOperationException("Cannot transfer a released character assembly.");
            }

            ownsAssembly = false;
        }

        public void Dispose()
        {
            if (!ownsAssembly)
            {
                return;
            }

            ownsAssembly = false;
            if (Root != null)
            {
                Root.SetActive(false);
            }

            Character?.ShuttingDownPawn();
            PawnHost?.UnbindingPawn();
            if (Motor != null)
            {
                Motor.CharacterController = null;
            }

            if (Root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(Root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }
}
