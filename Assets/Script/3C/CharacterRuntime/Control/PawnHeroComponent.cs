using System.Linq;
using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.GameplayTags;

namespace CGame
{
    public sealed class PawnHeroComponent : ActorComponent
    {
        private readonly InputProfile inputProfile;
        private readonly List<IDisposable> bindings = new List<IDisposable>();
        private AbilitySystemComponent abilitySystem;
        private Pawn pawn;

        public PawnHeroComponent(InputProfile inputProfile)
        {
            this.inputProfile = inputProfile;
        }

        public bool IsBound => abilitySystem != null;
        public bool HasInputProfile => inputProfile != null;

        public void Bind(InputHandle inputHandle, AbilitySystemComponent abilitySystem, Pawn pawn = null)
        {
            if (inputHandle == null) throw new ArgumentNullException(nameof(inputHandle));
            if (abilitySystem == null) throw new ArgumentNullException(nameof(abilitySystem));
            if (IsBound) throw new InvalidOperationException("PawnHeroComponent is already bound.");
            if (inputProfile == null) return;
            if (!GameplayTagManager.Instance.IsInitialized) return;
            if (!inputProfile.TryValidate(out string error)) throw new InvalidOperationException(error ?? "Pawn requires a valid InputProfile.");

            try
            {
                foreach (InputTagBinding binding in inputProfile.InputTagConfig.Bindings)
                {
                    bool isAimAction = string.Equals(binding.ActionReference?.action?.name, "Aim", StringComparison.Ordinal);
                    bindings.Add(inputHandle.RegisterActionCallback(binding.ActionReference, InputCallbackPhase.Performed, _ =>
                    {
                        abilitySystem.AbilityInputTagPressed(binding.InputTag);
                        if (isAimAction) pawn?.SetAimingFromInput(true);
                    }));
                    bindings.Add(inputHandle.RegisterActionCallback(binding.ActionReference, InputCallbackPhase.Canceled, _ =>
                    {
                        abilitySystem.AbilityInputTagReleased(binding.InputTag);
                        if (isAimAction) pawn?.SetAimingFromInput(false);
                    }));
                }
                if (inputProfile.TryResolveAction("Aim", out UnityEngine.InputSystem.InputAction aimAction)
                    && !inputProfile.InputTagConfig.Bindings.Any(binding =>
                        string.Equals(binding.ActionReference?.action?.name, "Aim", StringComparison.Ordinal)))
                {
                    UnityEngine.InputSystem.InputActionReference aimReference =
                        UnityEngine.InputSystem.InputActionReference.Create(aimAction);
                    bindings.Add(inputHandle.RegisterActionCallback(
                        aimReference, InputCallbackPhase.Performed, _ => pawn?.SetAimingFromInput(true)));
                    bindings.Add(inputHandle.RegisterActionCallback(
                        aimReference, InputCallbackPhase.Canceled, _ => pawn?.SetAimingFromInput(false)));
                }                this.pawn = pawn;

                this.abilitySystem = abilitySystem;
            }
            catch
            {
                Unbind();
                throw;
            }
        }

        public void Unbind()
        {
            for (int index = bindings.Count - 1; index >= 0; index--) bindings[index].Dispose();
            bindings.Clear();
            abilitySystem?.ClearAbilityInput();
            abilitySystem = null;
            pawn = null;
        }

        protected override void OnShutdown() => Unbind();



}
}
