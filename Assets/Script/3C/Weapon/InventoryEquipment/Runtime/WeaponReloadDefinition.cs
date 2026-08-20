using System;
using System.Collections.Generic;
using CGame.Animation;
using CGame.GameplayTags;
using UnityEngine;
using UnityEngine.Serialization;

namespace CGame.InventoryEquipment
{
    [Serializable]
    public sealed class WeaponReloadDefinition
    {
        // Keep the former ability-owned settings only long enough to migrate existing assets.
        // They are hidden so new authoring is exclusively owned by AbilityDefinition.
        [SerializeField, HideInInspector, FormerlySerializedAs("abilityTag")]
        private GameplayTag legacyAbilityTag;
        [SerializeField, HideInInspector, FormerlySerializedAs("inputTag")]
        private GameplayTag legacyInputTag;
        [SerializeField, HideInInspector, FormerlySerializedAs("cancelAbilityTags")]
        private GameplayTag[] legacyCancelAbilityTags = Array.Empty<GameplayTag>();
        [SerializeField, HideInInspector, FormerlySerializedAs("blockedOwnedTags")]
        private GameplayTag[] legacyBlockedOwnedTags = Array.Empty<GameplayTag>();
        [SerializeField] private GameplayTag commitEventTag;
        [SerializeField] private GameplayTag reloadingStateTag;
        [SerializeField] private AnimationClipAsset characterAnimation;
        [SerializeField] private AnimationClip weaponAnimation;

        public GameplayTag CommitEventTag => commitEventTag;
        public GameplayTag ReloadingStateTag => reloadingStateTag;
        public AnimationClipAsset CharacterAnimation => characterAnimation;
        public AnimationClip WeaponAnimation => weaponAnimation;
        internal GameplayTag LegacyAbilityTag => legacyAbilityTag;
        internal GameplayTag LegacyInputTag => legacyInputTag;
        internal IReadOnlyList<GameplayTag> LegacyCancelAbilityTags => legacyCancelAbilityTags;
        internal IReadOnlyList<GameplayTag> LegacyBlockedOwnedTags => legacyBlockedOwnedTags;

        public bool IsConfigured =>
            !commitEventTag.IsEmpty
            || !reloadingStateTag.IsEmpty
            || characterAnimation != null
            || weaponAnimation != null;

        public void Configure(
            GameplayTag newCommitEventTag,
            GameplayTag newReloadingStateTag,
            AnimationClipAsset newCharacterAnimation,
            AnimationClip newWeaponAnimation)
        {
            commitEventTag = newCommitEventTag;
            reloadingStateTag = newReloadingStateTag;
            characterAnimation = newCharacterAnimation;
            weaponAnimation = newWeaponAnimation;
        }

        public void Validate()
        {
            if (commitEventTag.IsEmpty || reloadingStateTag.IsEmpty)
            {
                throw new InvalidOperationException("Reload Definition requires Commit Event and Reloading State tags.");
            }

            if (characterAnimation == null || !characterAnimation.IsValid)
            {
                throw new InvalidOperationException("Reload Definition requires a valid character animation asset.");
            }

            if (weaponAnimation == null || weaponAnimation.legacy)
            {
                throw new InvalidOperationException("Reload Definition requires a non-legacy weapon animation clip.");
            }
        }

        internal void ClearLegacyAbilityConfiguration()
        {
            legacyAbilityTag = default;
            legacyInputTag = default;
            legacyCancelAbilityTags = Array.Empty<GameplayTag>();
            legacyBlockedOwnedTags = Array.Empty<GameplayTag>();
        }

    }
}
