using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.GameplayTags;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame
{
    public sealed class PawnHeroComponent : ActorComponent
    {
        private readonly InputProfile inputProfile;
        private readonly List<IDisposable> bindings = new List<IDisposable>();
        private AbilitySystemComponent abilitySystem;
        private Pawn pawn;
        private GameplayTag meleeInputTag;

        public PawnHeroComponent(InputProfile inputProfile)
        {
            this.inputProfile = inputProfile;
        }

        public bool IsBound => abilitySystem != null;
        public bool HasInputProfile => inputProfile != null;

        protected override void OnInitialize()
        {
            AddTickTask("Pawn.HeroMeleeInput", TickGroup.TG_Input, TickMeleeInput);
        }

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
                    if (binding.ActionReference?.action?.name == "Melee") meleeInputTag = binding.InputTag;
                    bindings.Add(inputHandle.RegisterActionCallback(binding.ActionReference, InputCallbackPhase.Started, _ =>
                    {
                        if (!Application.isFocused) return;
                        if (binding.InputTag.ToString() == "InputTag.Weapon.Fire")
                        {
                            Debug.Log("[CueDebug] Input Started -> InputTag.Weapon.Fire");
                        }
                        else if (binding.InputTag.ToString() == "InputTag.Weapon.Reload")
                        {
                            Debug.Log("[ReloadTrace] Input Started -> InputTag.Weapon.Reload");
                        }
                        abilitySystem.AbilityInputTagPressed(binding.InputTag);
                        // Input callbacks run after the controller's per-frame input
                        // pass in this runtime. Process immediately so a queued
                        // InputSystem press cannot be erased by its release before
                        // the next controller tick observes it.
                        abilitySystem.ProcessAbilityInput();
                    }));
                    bindings.Add(inputHandle.RegisterActionCallback(binding.ActionReference, InputCallbackPhase.Canceled, _ =>
                    {
                        if (!Application.isFocused) return;
                        abilitySystem.AbilityInputTagReleased(binding.InputTag);
                        abilitySystem.ProcessAbilityInput();
                    }));
                }
                this.pawn = pawn;

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
            meleeInputTag = default;
        }

        private void TickMeleeInput(float deltaTime)
        {
            if (!Application.isFocused || abilitySystem == null || meleeInputTag.IsEmpty || Keyboard.current == null) return;
            if (Keyboard.current.vKey.wasPressedThisFrame) abilitySystem.AbilityInputTagPressed(meleeInputTag);
            if (Keyboard.current.vKey.wasReleasedThisFrame) abilitySystem.AbilityInputTagReleased(meleeInputTag);
        }

        protected override void OnShutdown() => Unbind();



}
}
