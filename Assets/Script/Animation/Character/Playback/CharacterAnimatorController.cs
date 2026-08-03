using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation
{
    public sealed class CharacterAnimatorController
    {
        private const float LocomotionParameterSmoothing = 14f;

        private readonly struct ParameterContract
        {
            public ParameterContract(string name, AnimatorControllerParameterType type)
            {
                Name = name;
                Hash = Animator.StringToHash(name);
                Type = type;
            }

            public string Name { get; }
            public int Hash { get; }
            public AnimatorControllerParameterType Type { get; }
        }

        private static readonly ParameterContract[] parameterContracts =
        {
            new ParameterContract("MoveX", AnimatorControllerParameterType.Float),
            new ParameterContract("MoveY", AnimatorControllerParameterType.Float),
            new ParameterContract("Velocity", AnimatorControllerParameterType.Float),
            new ParameterContract("Moving", AnimatorControllerParameterType.Bool),
            new ParameterContract("InAir", AnimatorControllerParameterType.Bool),
            new ParameterContract("Sprinting", AnimatorControllerParameterType.Float),
        };

        private static readonly HashSet<int> reportedInvalidControllers = new HashSet<int>();
        private readonly Animator animator;
        private readonly AnimationUpdateContext context;
        private RuntimeAnimatorController runtimeController;
        private Vector2 moveDirection;
        private float velocity;
        private bool moving;
        private bool inAir;
        private float sprinting;
        private bool parametersValid;

        public CharacterAnimatorController(Animator animator, AnimationUpdateContext context)
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal RuntimeAnimatorController RuntimeController => runtimeController;

        public bool IsValid()
        {
            return animator != null
                && parametersValid
                && runtimeController != null
                && animator.runtimeAnimatorController == runtimeController;
        }

        internal Animator Animator => animator;

        public bool TryBind()
        {
            parametersValid = false;
            runtimeController = null;
            if (animator == null
                || !animator.isActiveAndEnabled
                || animator.runtimeAnimatorController == null
                || !animator.playableGraph.IsValid()
                || animator.playableGraph.GetOutputCount() == 0)
            {
                return false;
            }

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int contractIndex = 0; contractIndex < parameterContracts.Length; contractIndex++)
            {
                ParameterContract contract = parameterContracts[contractIndex];
                bool found = false;
                for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    AnimatorControllerParameter parameter = parameters[parameterIndex];
                    if (parameter.nameHash == contract.Hash && parameter.type == contract.Type)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    continue;
                }

                int controllerId = controller.GetInstanceID();
                if (reportedInvalidControllers.Add(controllerId))
                {
                    Debug.LogError(
                        $"Animator Controller '{controller.name}' requires "
                        + $"{contract.Name} ({contract.Type}) for character locomotion.");
                }

                return false;
            }

            runtimeController = controller;
            parametersValid = true;
            return true;
        }

        public void UpdateParameters(float deltaTime)
        {
            if (!IsValid() || deltaTime <= 0f)
            {
                return;
            }

            bool isGrounded = context.CharacterState.IsGrounded;
            moving = context.CharacterState.IsMoving;
            Vector2 targetDirection = isGrounded && moving
                ? context.Velocity.NormalizedMoveDirection
                : Vector2.zero;
            float blendAlpha = 1f - Mathf.Exp(-LocomotionParameterSmoothing * deltaTime);
            moveDirection = Vector2.Lerp(moveDirection, targetDirection, blendAlpha);
            velocity = moveDirection.magnitude;
            inAir = !isGrounded;
            sprinting = Mathf.Lerp(
                sprinting,
                context.CharacterState.IsSprinting ? 1f : 0f,
                blendAlpha);

            animator.SetFloat(parameterContracts[0].Hash, moveDirection.x);
            animator.SetFloat(parameterContracts[1].Hash, moveDirection.y);
            animator.SetFloat(parameterContracts[2].Hash, velocity);
            animator.SetBool(parameterContracts[3].Hash, moving);
            animator.SetBool(parameterContracts[4].Hash, inAir);
            animator.SetFloat(parameterContracts[5].Hash, sprinting);
        }

    }
}
