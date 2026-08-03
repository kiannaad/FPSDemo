using System;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame.Ability.Animation
{
    [Serializable]
    public sealed class AnimationGameEventNotify : global::CGame.Animation.AnimationInstantNotify
    {
        [SerializeField] private GameplayTag eventTag;

        public GameplayTag EventTag
        {
            get => eventTag;
            set => eventTag = value;
        }

        public override void OnNotify(Pawn pawn)
        {
            AbilitySystemComponent abilitySystem = pawn?.AbilitySystem;
            if (abilitySystem == null)
            {
                return;
            }

            abilitySystem.HandleGameEvent(
                eventTag,
                new AbilityGameEventPayload(abilitySystem, pawn, null, default));
        }
    }
}
