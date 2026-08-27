using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame
{
    public class PawnFactory
    {
        public virtual async Task<Pawn> CreateAsync(
            PawnDefinition definition,
            Vector3 position,
            Quaternion rotation,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.PawnPrefab == null)
            {
                throw new InvalidOperationException("PawnDefinition must specify exactly one PawnPrefab.");
            }

            await PrepareResourcesAsync(definition, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            GameObject root = null;
            try
            {
                root = UnityEngine.Object.Instantiate(definition.PawnPrefab, position, rotation);
                root.name = definition.PawnPrefab.name;
                root.SetActive(false);
                ConfigureFirstPersonVisual(root, definition);

                CharacterPhysicsMotor motor = root.GetComponent<CharacterPhysicsMotor>();
                Animator animator = ResolveAnimator(root);
                Camera camera = root.GetComponentInChildren<Camera>(true);
                KRigComponent rigComponent = ResolveRigComponent(animator, definition.Rig);
                root.name = $"Pawn:{definition.name}";
                var components = new List<ActorComponent>
                {
                    new PawnMovementComponent(motor),
                    new PawnAnimationComponent(animator, motor, definition.AnimationConfig, rigComponent),
                    new EquipmentManagerComponent(),
                    new PawnHeroComponent(definition.InputProfile),
                    new PawnCameraComponent(camera, definition.RequireCamera),
                    new PawnShotQueryComponent()
                };
                return new Pawn(root, components);
            }
            catch
            {
                DestroyRoot(root);
                throw;
            }
        }

        protected virtual Task PrepareResourcesAsync(
            PawnDefinition definition,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        private static void ConfigureFirstPersonVisual(GameObject root, PawnDefinition definition)
        {
            if (definition.FirstPersonMesh == null || definition.FirstPersonMaterial == null)
            {
                return;
            }

            SkinnedMeshRenderer renderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null)
            {
                throw new InvalidOperationException(
                    "PawnDefinition specifies a first-person mesh, but the PawnPrefab has no SkinnedMeshRenderer.");
            }

            renderer.sharedMesh = definition.FirstPersonMesh;
            renderer.sharedMaterials = new[] { definition.FirstPersonMaterial };
        }

        private static Animator ResolveAnimator(GameObject root)
        {
            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1)
            {
                throw new InvalidOperationException(
                    "PawnPrefab must contain exactly one Animator in its hierarchy.");
            }

            return animators[0];
        }

        private static KRigComponent ResolveRigComponent(Animator animator, KRig rig)
        {
            if (rig == null)
            {
                throw new InvalidOperationException("PawnDefinition must specify a KRig.");
            }

            KRigComponent[] rigComponents = animator.GetComponentsInChildren<KRigComponent>(true);
            if (rigComponents.Length != 1)
            {
                throw new InvalidOperationException(
                    "Pawn Animator hierarchy must contain exactly one KRigComponent.");
            }

            rigComponents[0].Initialize(rig);
            return rigComponents[0];
        }

        private static void DestroyRoot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
