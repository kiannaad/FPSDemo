using System.Linq;
using CGame.Ability;
using CGame.Ability.Animation;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class AK12ReloadDefinitionTests
    {
        private const string WeaponDefinitionPath =
            "Assets/Settings/Gameplay/Weapon/WeaponDefinition/AK12/AK12WeaponDefinition.asset";
        private const string WeaponItemDefinitionPath =
            "Assets/Settings/Gameplay/Weapon/WeaponDefinition/AK12/AK12WeaponItemDefinition.asset";

        [Test]
        public void Ak12ReloadDefinition_HasCompleteInputAnimationAndCommitData()
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponDefinitionPath);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.ReloadDefinition, Is.Not.Null);
            Assert.That(definition.ReloadDefinition.IsConfigured, Is.True);
            definition.ReloadDefinition.Validate();
            Assert.That(definition.ReloadDefinition.CommitEventTag.ToString(), Is.EqualTo("Event.Weapon.Reload.Commit"));
            Assert.That(definition.ReloadDefinition.ReloadingStateTag.ToString(), Is.EqualTo("State.Weapon.Reloading"));

            ReloadWeaponAbilityDefinition reloadAbility = definition.AbilitySet.Abilities
                .OfType<ReloadWeaponAbilityDefinition>()
                .Single();
            Assert.That(reloadAbility.AbilityTag.ToString(), Is.EqualTo("Ability.Weapon.Reload"));
            Assert.That(reloadAbility.InputTag.ToString(), Is.EqualTo("InputTag.Weapon.Reload"));
            Assert.That(reloadAbility.ActivationOwnedTags.Select(tag => tag.ToString()),
                Is.EquivalentTo(new[] { "State.Weapon.Action" }));
            Assert.That(reloadAbility.CancelAbilityTags.Select(tag => tag.ToString()),
                Is.EquivalentTo(new[] { "Ability.Weapon.Fire", "Ability.Weapon.Aim" }));
            Assert.That(reloadAbility.BlockedOwnedTags.Select(tag => tag.ToString()),
                Is.EquivalentTo(new[] { "State.Weapon.Switch" }));

            AnimationNotifyEvent commit = definition.ReloadDefinition.CharacterAnimation.NotifyTracks
                .Single(track => track.Name == "Gameplay")
                .Events.Single();
            Assert.That(commit.StartFrame, Is.EqualTo(70));
            Assert.That(commit.DurationFrames, Is.Zero);
            Assert.That(commit.Notify, Is.TypeOf<AnimationGameEventNotify>());
            Assert.That(((AnimationGameEventNotify)commit.Notify).EventTag.ToString(),
                Is.EqualTo("Event.Weapon.Reload.Commit"));

            AbilityGrantDefinition reloadGrant = definition.CreateAbilitySet().Grants
                .Single(grant => grant.InputTag == reloadAbility.InputTag);
            Assert.That(reloadGrant.Definition.AbilityTag, Is.EqualTo(reloadAbility.AbilityTag));
            Assert.That(reloadGrant.Definition.ActivationOwnedTags.Select(tag => tag.ToString()),
                Is.EquivalentTo(new[] { "State.Weapon.Action", "State.Weapon.Reloading" }));
            Assert.That(reloadGrant.Definition.BlockedOwnedTags.Select(tag => tag.ToString()),
                Is.EquivalentTo(new[] { "State.Weapon.Switch" }));
            Assert.That(reloadGrant.Definition.CancelAbilityTags.Select(tag => tag.ToString()),
                Is.EquivalentTo(new[] { "Ability.Weapon.Fire", "Ability.Weapon.Aim" }));
        }

        [Test]
        public void Ak12WeaponItemDefinition_PreservesConfiguredAmmo()
        {
            WeaponItemDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>(WeaponItemDefinitionPath);
            var inventory = new InventoryComponent();
            try
            {
                ItemInstanceHandle handle = inventory.Add(definition);
                Assert.That(inventory.TryGet(handle, out ItemInstance item), Is.True);
                Assert.That(item.MagazineAmmo, Is.EqualTo(30));
                Assert.That(item.ReserveAmmo, Is.EqualTo(1000));
            }
            finally
            {
                inventory.Dispose();
            }
        }

        [Test]
        public void Ak12ReloadCharacterAnimation_KeepsAnimationOwnershipUntilBlendOut()
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponDefinitionPath);
            AnimationClipAsset animation = definition.ReloadDefinition.CharacterAnimation;

            Assert.That(animation.TryGetNamedCurve("WeaponBoneWeight", out AnimationCurve weaponBoneWeight), Is.True);
            Assert.That(weaponBoneWeight.Evaluate(0f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(weaponBoneWeight.Evaluate(0.5f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(weaponBoneWeight.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));

            Assert.That(animation.TryGetNamedCurve("MaskAttachHand", out AnimationCurve maskAttachHand), Is.True);
            Assert.That(maskAttachHand.Evaluate(0f), Is.Zero.Within(0.0001f));
            Assert.That(maskAttachHand.Evaluate(0.5f), Is.Zero.Within(0.0001f));
            Assert.That(maskAttachHand.Evaluate(1f), Is.Zero.Within(0.0001f));
        }
    }
}
