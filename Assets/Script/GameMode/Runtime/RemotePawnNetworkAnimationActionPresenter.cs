using System;
using System.Collections.Generic;
using CGame.Animation;
using CGame.InventoryEquipment;
using CGame.Network;

namespace CGame
{
    public sealed class RemotePawnNetworkAnimationActionPresenter : INetworkAnimationActionPresenter
    {
        private readonly PawnAnimationComponent animation;
        private readonly Dictionary<string, AnimationClipAsset> clips = new Dictionary<string, AnimationClipAsset>();
        private readonly Dictionary<string, IkMotionLayerSettings> motions = new Dictionary<string, IkMotionLayerSettings>();

        public RemotePawnNetworkAnimationActionPresenter(PawnAnimationComponent animation, InitialInventorySet inventory)
        {
            this.animation = animation ?? throw new ArgumentNullException(nameof(animation));
            if (inventory == null) return;
            foreach (ItemDefinition item in inventory.ItemDefinitions)
            {
                if (!(item is WeaponItemDefinition weaponItem) || weaponItem.WeaponDefinition == null) continue;
                WeaponDefinition weapon = weaponItem.WeaponDefinition;
                AddClip(weapon.ReloadDefinition?.CharacterAnimation);
                foreach (WeaponAbilityDefinition ability in weapon.AbilitySet.Abilities)
                    if (ability is MeleeWeaponAbilityDefinition melee) AddClip(melee.AttackClip);
                AddMotion(weapon.EquipIkMotion);
                AddMotion(weapon.UnequipIkMotion);
            }

            int selectedSlot = inventory.SelectedSlot;
            if (selectedSlot >= 0 && selectedSlot < inventory.ItemDefinitions.Count
                && inventory.ItemDefinitions[selectedSlot] is WeaponItemDefinition selectedWeapon
                && selectedWeapon.WeaponDefinition?.ArmedProfile != null)
                animation.AnimInstance?.BoneController.LinkProfile(selectedWeapon.WeaponDefinition.ArmedProfile);
        }

        public bool TryPlay(NetworkAnimationActionStarted action, float elapsedSeconds, out object playbackHandle)
        {
            playbackHandle = null;
            CharacterAnimInstance instance = animation.AnimInstance;
            if (instance == null || action == null || string.IsNullOrWhiteSpace(action.VariantId)) return false;
            if (clips.TryGetValue(action.VariantId, out AnimationClipAsset clip))
            {
                AnimationPlaybackHandle handle = instance.PlayAbilityAnimationAtSeconds(clip, action.ActionSequence, elapsedSeconds);
                if (handle == null || handle.State == AnimationPlaybackState.Failed) return false;
                playbackHandle = handle;
                return true;
            }
            if (!motions.TryGetValue(action.VariantId, out IkMotionLayerSettings motion)
                || !instance.TryPlayWeaponIkMotionAtSeconds(motion, elapsedSeconds)) return false;
            playbackHandle = motion;
            return true;
        }

        public void Stop(object playbackHandle)
        {
            if (playbackHandle is AnimationPlaybackHandle clipHandle)
                animation.AnimInstance?.StopAbilityAnimation(clipHandle);
            else if (playbackHandle is IkMotionLayerSettings motion)
                animation.AnimInstance?.TryStopWeaponIkMotion(motion);
        }

        private void AddClip(AnimationClipAsset clip)
        {
            if (clip != null && !clips.ContainsKey(clip.name)) clips.Add(clip.name, clip);
        }

        private void AddMotion(IkMotionLayerSettings motion)
        {
            if (motion != null && !motions.ContainsKey(motion.name)) motions.Add(motion.name, motion);
        }
    }
}
