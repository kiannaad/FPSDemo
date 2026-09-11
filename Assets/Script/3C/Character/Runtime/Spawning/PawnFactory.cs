using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CGame.Animation;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame
{
    public class PawnFactory
    {
        public virtual async Task<Pawn> CreateAsync(
            PawnDefinition definition,
            InputProfile inputProfile,
            Vector3 position,
            Quaternion rotation,
            CancellationToken cancellationToken = default)
        {
            return await CreateConfiguredAsync(definition, inputProfile, position, rotation, true, null, null, cancellationToken);
        }

        public Task<Pawn> CreateAsync(
            PawnDefinition definition,
            InputProfile inputProfile,
            Vector3 position,
            Quaternion rotation,
            ActorComponent supplementalComponent,
            CancellationToken cancellationToken = default)
        {
            return CreateConfiguredAsync(
                definition,
                inputProfile,
                position,
                rotation,
                true,
                supplementalComponent,
                null,
                cancellationToken);
        }

        public virtual Task<Pawn> CreateRemoteAsync(
            PawnDefinition definition,
            Vector3 position,
            Quaternion rotation,
            CancellationToken cancellationToken = default)
        {
            return CreateRemoteAsync(definition, position, rotation, null, cancellationToken);
        }

        public virtual Task<Pawn> CreateRemoteAsync(
            PawnDefinition definition,
            Vector3 position,
            Quaternion rotation,
            ActorComponent supplementalComponent,
            CancellationToken cancellationToken = default)
        {
            return CreateRemoteAsync(definition, position, rotation, supplementalComponent, null, cancellationToken);
        }

        public virtual Task<Pawn> CreateRemoteAsync(
            PawnDefinition definition,
            Vector3 position,
            Quaternion rotation,
            ActorComponent supplementalComponent,
            Func<Transform, IAnimationCharacterSource> characterSourceFactory,
            CancellationToken cancellationToken = default)
        {
            return CreateConfiguredAsync(
                definition,
                null,
                position,
                rotation,
                false,
                supplementalComponent,
                characterSourceFactory,
                cancellationToken);
        }

        private async Task<Pawn> CreateConfiguredAsync(
            PawnDefinition definition,
            InputProfile inputProfile,
            Vector3 position,
            Quaternion rotation,
            bool includeLocalComponents,
            ActorComponent supplementalComponent,
            Func<Transform, IAnimationCharacterSource> characterSourceFactory,
            CancellationToken cancellationToken)
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
                if (includeLocalComponents)
                {
                    ConfigureFirstPersonVisual(root, definition);
                }
                else
                {
                    ConfigureRemoteVisual(root);
                }

                CharacterPhysicsMotor motor = root.GetComponent<CharacterPhysicsMotor>();
                Animator animator = ResolveAnimator(root);
                KRigComponent rigComponent = ResolveRigComponent(animator, definition.Rig);
                root.name = $"Pawn:{definition.name}";
                var components = new List<ActorComponent>
                {
                    new PawnMovementComponent(motor),
                    new PawnAnimationComponent(
                        animator,
                        motor,
                        definition.AnimationConfig,
                        rigComponent,
                        characterSourceFactory?.Invoke(motor != null ? motor.transform : root.transform))
                };
                if (supplementalComponent != null)
                {
                    components.Add(supplementalComponent);
                }
                if (includeLocalComponents)
                {
                    Camera camera = root.GetComponentInChildren<Camera>(true);
                    components.Add(new EquipmentManagerComponent());
                    components.Add(new PawnHeroComponent(inputProfile));
                    components.Add(new PawnCameraComponent(camera, definition.RequireCamera));
                    components.Add(new PawnShotQueryComponent());
                }
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

        private static void ConfigureRemoteVisual(GameObject root)
        {
            Camera firstPersonCamera = root.GetComponentInChildren<Camera>(true);
            if (firstPersonCamera != null)
            {
                // Local head-hiding layers belong to the owner view, not to a
                // remote character seen through another pawn's identical camera.
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if ((firstPersonCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                        renderer.gameObject.layer = root.layer;
            }
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = false;
            }

            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
            {
                listener.enabled = false;
            }
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
