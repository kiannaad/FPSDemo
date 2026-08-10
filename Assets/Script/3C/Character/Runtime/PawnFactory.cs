using System;
using UnityEngine;

namespace CGame
{
    public class PawnFactory
    {
        public virtual PawnAssembly CreateCandidate(
            PawnData pawnData,
            Vector3 position,
            Quaternion rotation)
        {
            if (pawnData == null)
            {
                throw new ArgumentNullException(nameof(pawnData));
            }

            if (pawnData.PawnPrefab == null)
            {
                throw new InvalidOperationException("PawnData must specify exactly one PawnPrefab.");
            }

            GameObject root = null;
            try
            {
                root = UnityEngine.Object.Instantiate(pawnData.PawnPrefab, position, rotation);
                root.name = $"PawnCandidate:{pawnData.name}";
                root.SetActive(false);
                PawnHost host = root.GetComponent<PawnHost>() ?? root.AddComponent<PawnHost>();
                host.Animator = root.GetComponentInChildren<Animator>(true);
                host.MeshRoot = host.Animator != null ? host.Animator.transform : root.transform;
                configureFirstPersonVisual(root, pawnData);
                var pawn = new Pawn();
                host.BindingPawn(pawn);
                var extension = new PawnExtensionComponent(pawn, pawnData);
                CharacterPhysicsMotor motor = null;
                if (World.Current?.CharacterMotorSimulation is ICharacterPhysicsWorld)
                {
                    motor = root.GetComponent<CharacterPhysicsMotor>()
                        ?? root.AddComponent<CharacterPhysicsMotor>();
                }

                var movement = motor == null
                    ? new PawnMovementComponent(pawn)
                    : new PawnMovementComponent(pawn, motor);
                var animation = host.Animator != null
                    && motor != null
                    && pawnData.AnimationConfig != null
                        ? new PawnAnimationComponent(
                            pawn,
                            host.Animator,
                            motor,
                            pawnData.AnimationConfig)
                        : new PawnAnimationComponent();
                var equipment = new EquipmentManagerComponent();
                extension.RegisterParticipant(movement);
                extension.RegisterParticipant(animation);
                extension.RegisterParticipant(equipment);
                return new PawnAssembly(
                    root,
                    pawn,
                    host,
                    extension,
                    movement,
                    animation,
                    equipment);
            }
            catch
            {
                if (root != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(root);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }

                throw;
            }
        }

        private static void configureFirstPersonVisual(GameObject root, PawnData pawnData)
        {
            if (pawnData.FirstPersonMesh == null || pawnData.FirstPersonMaterial == null)
            {
                return;
            }

            SkinnedMeshRenderer renderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null)
            {
                throw new InvalidOperationException(
                    "PawnData specifies a first-person mesh, but the PawnPrefab has no SkinnedMeshRenderer.");
            }

            renderer.sharedMesh = pawnData.FirstPersonMesh;
            renderer.sharedMaterials = new[] { pawnData.FirstPersonMaterial };
        }
    }
}
