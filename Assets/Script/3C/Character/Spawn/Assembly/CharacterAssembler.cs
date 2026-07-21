using System;
using CGame.Animation;
using UnityEngine;

namespace CGame
{
    public sealed class CharacterAssembler
    {
        public CharacterAssembly Assemble(
            CharacterDefinition definition,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            string name)
        {
            if (definition == null || !definition.IsValid)
            {
                throw new ArgumentException("A valid character definition is required.", nameof(definition));
            }

            return Assemble(definition.VisualPrefab, definition.AnimationConfig, parent, position, rotation, name);
        }

        internal CharacterAssembly Assemble(
            GameObject visualPrefab,
            CharacterAnimationConfig animationConfig,
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

            GameObject root = null;
            CharacterAssembly assembly = null;
            try
            {
                root = new GameObject(string.IsNullOrWhiteSpace(name) ? "RuntimeCharacter" : name);
                root.SetActive(false);
                root.transform.SetParent(parent);
                root.transform.SetLocalPositionAndRotation(position, rotation);

                Animator animator = CreateVisual(root.transform, visualPrefab);
                PawnHost pawnHost = root.AddComponent<PawnHost>();
                CharacterPhysicsMotor motor = root.AddComponent<CharacterPhysicsMotor>();
                var character = new Character();
                var movement = new MovementComp();
                var animationComponent = new CharacterAnimationComponent(animator, motor, movement, animationConfig);
                movement.BindingMotor(motor);
                pawnHost.MeshRoot = animator.transform;
                pawnHost.Animator = animator;
                pawnHost.BindingPawn(character);
                character.RegisteringComponent(movement);
                character.RegisteringComponent(animationComponent);
                motor.CharacterController = movement;

                assembly = new CharacterAssembly(root, animator, character, pawnHost, motor, movement, animationComponent);
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
            GameObject visual = UnityEngine.Object.Instantiate(visualPrefab, parent);
            visual.name = "CharacterVisual";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>())
            {
                DestroyObject(collider);
            }

            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException("Configured character prefab does not contain an Animator.");
            }

            animator.applyRootMotion = false;
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
