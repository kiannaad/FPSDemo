using System;
using CGame.Animation;
using UnityEngine;

namespace CGame
{
    public sealed class CharacterAssembler
    {
        public CharacterAssembly Assemble(
            CharacterDefinition definition,
            WeaponAnimationDefinition initialWeaponDefinition,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            string name)
        {
            if (definition == null || !definition.IsValid)
            {
                throw new ArgumentException("A valid character definition is required.", nameof(definition));
            }

            if (initialWeaponDefinition == null
                || initialWeaponDefinition.Validate(definition.InitialWeaponId)
                    != WeaponAnimationDefinitionError.None)
            {
                throw new ArgumentException(
                    "A matching valid initial weapon definition is required.",
                    nameof(initialWeaponDefinition));
            }

            return Assemble(
                definition.VisualPrefab,
                definition.AnimationConfig,
                initialWeaponDefinition,
                parent,
                position,
                rotation,
                name);
        }

        internal CharacterAssembly Assemble(
            GameObject visualPrefab,
            CharacterAnimationConfig animationConfig,
            WeaponAnimationDefinition initialWeaponDefinition,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            string name)
        {
            if (visualPrefab == null)
            {
                throw new ArgumentNullException(nameof(visualPrefab));
            }

            if (animationConfig == null || !animationConfig.IsValid)
            {
                throw new ArgumentException("A valid character animation config is required.", nameof(animationConfig));
            }

            if (initialWeaponDefinition == null
                || !initialWeaponDefinition.IsValid)
            {
                throw new ArgumentException(
                    "A valid initial weapon definition is required.",
                    nameof(initialWeaponDefinition));
            }

            GameObject root = null;
            CharacterAssembly assembly = null;
            try
            {
                root = new GameObject(string.IsNullOrWhiteSpace(name) ? "RuntimeCharacter" : name);
                root.transform.SetParent(parent);
                root.transform.SetLocalPositionAndRotation(position, rotation);

                Animator animator = CreateVisual(root.transform, visualPrefab);
                PawnHost pawnHost = root.AddComponent<PawnHost>();
                CharacterPhysicsMotor motor = root.AddComponent<CharacterPhysicsMotor>();
                var character = new Character();
                var movement = new MovementComp();
                var animationComponent = new CharacterAnimationComponent(
                    animator,
                    motor,
                    animationConfig,
                    initialWeaponDefinition);
                movement.BindingMotor(motor);
                pawnHost.MeshRoot = animator.transform;
                pawnHost.Animator = animator;
                pawnHost.BindingPawn(character);
                character.RegisteringComponent(movement);
                character.RegisteringComponent(animationComponent);
                AnimationPlaybackHandle initialPoseHandle =
                    animationComponent.PrepareInitialWeaponPose(
                        initialWeaponDefinition.OverlayPose);
                if (initialPoseHandle == null
                    || initialPoseHandle.State
                        != AnimationPlaybackState.Playing)
                {
                    throw new InvalidOperationException(
                        "Initial weapon overlay could not be prepared at full weight.");
                }

                // Unity only exposes the Animator-owned native Output[0] while
                // the Animator is active. Assembly is synchronous, so no frame
                // elapses before the full-weight overlay is installed and the
                // unpublished root is made inactive.
                root.SetActive(false);
                motor.CharacterController = movement;

                assembly = new CharacterAssembly(
                    root,
                    animator,
                    character,
                    pawnHost,
                    motor,
                    movement,
                    animationComponent,
                    initialPoseHandle);
                return assembly;
            }
            catch
            {
                assembly?.Dispose();
                if (assembly == null)
                {
                    DestroyRoot(root);
                }

                throw;
            }
        }

        private static Animator CreateVisual(Transform parent, GameObject visualPrefab)
        {
            var visualRoot = new GameObject("CharacterVisual");
            visualRoot.transform.SetParent(parent, false);
            GameObject animatedVisual = UnityEngine.Object.Instantiate(
                visualPrefab,
                visualRoot.transform);
            animatedVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Collider collider in visualRoot.GetComponentsInChildren<Collider>())
            {
                DestroyObject(collider);
            }

            Animator animator = animatedVisual.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException("Configured character prefab does not contain an Animator.");
            }

            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
            return animator;
        }

        private static void DestroyRoot(GameObject root)
        {
            DestroyObject(root);
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
