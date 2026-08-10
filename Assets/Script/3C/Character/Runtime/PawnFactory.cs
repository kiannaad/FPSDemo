using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
                root.name = $"Pawn:{definition.name}";
                root.SetActive(false);
                ConfigureFirstPersonVisual(root, definition);

                CharacterPhysicsMotor motor = root.GetComponent<CharacterPhysicsMotor>();
                Animator animator = root.GetComponentInChildren<Animator>(true);
                Camera camera = root.GetComponentInChildren<Camera>(true);
                var components = new List<ActorComponent>
                {
                    new PawnMovementComponent(motor),
                    new PawnAnimationComponent(animator, motor, definition.AnimationConfig),
                    new EquipmentManagerComponent(),
                    new PawnHeroComponent(definition.InputProfile),
                    new PawnCameraComponent(camera, definition.RequireCamera)
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
