using System;
using UnityEngine;

namespace CGame
{
    public sealed class PawnAssembly : IDisposable
    {
        private bool disposed;

        internal PawnAssembly(
            GameObject root,
            Pawn pawn,
            PawnHost host,
            PawnExtensionComponent extension,
            PawnMovementComponent movement,
            PawnAnimationComponent animation,
            EquipmentManagerComponent equipment)
        {
            Root = root;
            Pawn = pawn;
            Host = host;
            Extension = extension;
            Movement = movement;
            Animation = animation;
            Equipment = equipment;
        }

        public GameObject Root { get; }

        public Pawn Pawn { get; }

        public PawnHost Host { get; }

        public PawnExtensionComponent Extension { get; }

        public PawnMovementComponent Movement { get; }

        public PawnAnimationComponent Animation { get; }

        public EquipmentManagerComponent Equipment { get; }

        public HeroComponent Hero { get; private set; }

        public bool IsDisposed => disposed;

        public void AddHero(PlayerController controller)
        {
            if (Hero != null)
            {
                throw new InvalidOperationException("PawnAssembly already has a HeroComponent.");
            }

            Hero = new HeroComponent(controller, Pawn, Equipment, Extension);
            Extension.RegisterParticipant(Hero);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (Root != null)
            {
                Root.SetActive(false);
            }

            Extension.Shutdown();
            Host?.UnbindingPawn();
            Pawn?.ShuttingDownPawn();
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
