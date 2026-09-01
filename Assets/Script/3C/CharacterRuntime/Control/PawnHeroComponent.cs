using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.GameplayTags;
using UnityEngine;

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
                    bindings.Add(inputHandle.RegisterActionCallback(binding.ActionReference, InputCallbackPhase.Started, _ =>
                    {
                        if (binding.InputTag.ToString() == "InputTag.Weapon.Fire")
                        {
                            Debug.Log("[CueDebug] Input Started -> InputTag.Weapon.Fire");
                        }
                        else if (binding.InputTag.ToString() == "InputTag.Weapon.Reload")
                        {
                            Debug.Log("[ReloadTrace] Input Started -> InputTag.Weapon.Reload");
                        }
                        abilitySystem.AbilityInputTagPressed(binding.InputTag);
                    }));
                    bindings.Add(inputHandle.RegisterActionCallback(binding.ActionReference, InputCallbackPhase.Canceled, _ =>
                    {
                        abilitySystem.AbilityInputTagReleased(binding.InputTag);
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
        }

        protected override void OnShutdown() => Unbind();



}
}
