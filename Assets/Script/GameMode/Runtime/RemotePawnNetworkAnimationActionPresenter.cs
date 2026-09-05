using System;
using System.Collections.Generic;
using CGame.Animation;
using CGame.InventoryEquipment;
using CGame.Network;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame
{
    public sealed class RemotePawnNetworkAnimationActionPresenter : INetworkAnimationActionPresenter
    {
        private readonly Pawn pawn;
        private readonly PawnAnimationComponent animation;
        private readonly Dictionary<string, AnimationClipAsset> clips = new Dictionary<string, AnimationClipAsset>();
        private readonly Dictionary<string, IkMotionLayerSettings> motions = new Dictionary<string, IkMotionLayerSettings>();
        private readonly Dictionary<long, WeaponDefinition> weaponsByInstanceId = new Dictionary<long, WeaponDefinition>();
        private GameObject weaponPresentation;
        private AnimationPlaybackHandle weaponOverlayHandle;

        public RemotePawnNetworkAnimationActionPresenter(Pawn pawn, PawnAnimationComponent animation, InitialInventorySet inventory)
        {
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            this.animation = animation ?? throw new ArgumentNullException(nameof(animation));
            if (inventory == null) return;
            for (int index = 0; index < inventory.ItemDefinitions.Count; index++)
            {
                ItemDefinition item = inventory.ItemDefinitions[index];
                if (!(item is WeaponItemDefinition weaponItem) || weaponItem.WeaponDefinition == null) continue;
                WeaponDefinition weapon = weaponItem.WeaponDefinition;
                weaponsByInstanceId[index + 1L] = weapon;
                AddClip(weapon.ReloadDefinition?.CharacterAnimation);
                foreach (WeaponAbilityDefinition ability in weapon.AbilitySet.Abilities)
                    if (ability is MeleeWeaponAbilityDefinition melee) AddClip(melee.AttackClip);
                AddMotion(weapon.EquipIkMotion);
                AddMotion(weapon.UnequipIkMotion);
            }

            int selectedSlot = inventory.SelectedSlot;
            if (selectedSlot >= 0 && selectedSlot < inventory.ItemDefinitions.Count
                && inventory.ItemDefinitions[selectedSlot] is WeaponItemDefinition selectedWeapon
                && selectedWeapon.WeaponDefinition != null)
            {
                WeaponDefinition weapon = selectedWeapon.WeaponDefinition;
                if (weapon.ArmedProfile != null)
                    animation.AnimInstance?.BoneController.LinkProfile(weapon.ArmedProfile);
                CreateInitialWeaponPresentation(weapon);
            }
        }

        public Pawn Pawn => pawn;

        public void AdvanceRecoil(float deltaTime)
        {
            pawn.AdvanceRecoil(deltaTime);
        }

        public bool TryPlay(NetworkAnimationActionStarted action, float elapsedSeconds, out object playbackHandle)
        {
            playbackHandle = null;
            CharacterAnimInstance instance = animation.AnimInstance;
            if (instance == null || action == null || string.IsNullOrWhiteSpace(action.VariantId)) return false;
            if (action.ActionKind == NetworkAnimationActionKind.Equip &&
                weaponsByInstanceId.TryGetValue(action.EquipmentInstanceId, out WeaponDefinition equippedWeapon))
            {
                ApplyWeaponPresentation(equippedWeapon);
            }

            RemoteWeaponReloadPlayback reloadPlayback = null;
            if (action.ActionKind == NetworkAnimationActionKind.Reload
                && weaponsByInstanceId.TryGetValue(action.EquipmentInstanceId, out WeaponDefinition reloadWeapon))
            {
                reloadPlayback = RemoteWeaponReloadPlayback.TryStart(weaponPresentation, reloadWeapon, elapsedSeconds);
            }
            if (clips.TryGetValue(action.VariantId, out AnimationClipAsset clip))
            {
                AnimationPlaybackHandle handle = instance.PlayAbilityAnimationAtSeconds(clip, action.ActionSequence, elapsedSeconds);
                if (handle == null || handle.State == AnimationPlaybackState.Failed)
                {
                    reloadPlayback?.Stop();
                    return false;
                }
                playbackHandle = reloadPlayback == null ? (object)handle : new CompositePlayback(handle, reloadPlayback);
                return true;
            }
            if (reloadPlayback != null)
            {
                playbackHandle = reloadPlayback;
                return true;
            }
            if (!motions.TryGetValue(action.VariantId, out IkMotionLayerSettings motion)
                || !instance.TryPlayWeaponIkMotionAtSeconds(motion, elapsedSeconds)) return false;
            playbackHandle = motion;
            return true;
        }

        public void Stop(object playbackHandle)
        {
            if (playbackHandle is CompositePlayback composite)
            {
                animation.AnimInstance?.StopAbilityAnimation(composite.CharacterPlayback);
                composite.WeaponReloadPlayback.Stop();
            }
            else if (playbackHandle is RemoteWeaponReloadPlayback reloadPlayback)
                reloadPlayback.Stop();
            else if (playbackHandle is AnimationPlaybackHandle clipHandle)
                animation.AnimInstance?.StopAbilityAnimation(clipHandle);
            else if (playbackHandle is IkMotionLayerSettings motion)
                animation.AnimInstance?.TryStopWeaponIkMotion(motion);
        }

        public bool ApplyCommittedFire(long equipmentInstanceId, string recoilProfileId)
        {
            if (equipmentInstanceId > 0 && weaponsByInstanceId.TryGetValue(equipmentInstanceId, out WeaponDefinition equippedWeapon))
            {
                if (equippedWeapon?.RecoilProfile == null)
                {
                    Debug.LogWarning($"[Network][045] RemoteRecoilRejected Pawn={pawn.Root.name} Equipment={equipmentInstanceId} Reason=ProfileMissing");
                    return false;
                }

                FireResult equippedResult = pawn.ApplySuccessfulShot(equippedWeapon.RecoilProfile);
                Debug.Log($"[Network][045] RemoteRecoilApplied Pawn={pawn.Root.name} Equipment={equipmentInstanceId} Profile={equippedWeapon.RecoilProfile.name} Succeeded={equippedResult.Succeeded} Sequence={equippedResult.ShotSequence}");
                return equippedResult.Succeeded;
            }

            WeaponDefinition weapon = null;
            foreach (WeaponDefinition candidate in weaponsByInstanceId.Values)
            {
                if (candidate?.RecoilProfile == null) continue;
                if (string.IsNullOrWhiteSpace(recoilProfileId) || recoilProfileId == "Default" ||
                    string.Equals(candidate.RecoilProfile.name, recoilProfileId, StringComparison.Ordinal))
                {
                    weapon = candidate;
                    break;
                }
                weapon ??= candidate;
            }
            if (weapon?.RecoilProfile == null)
            {
                Debug.LogWarning($"[Network][045] RemoteRecoilRejected Pawn={pawn.Root.name} Equipment={equipmentInstanceId} Reason=ProfileNotFound");
                return false;
            }

            FireResult fallbackResult = pawn.ApplySuccessfulShot(weapon.RecoilProfile);
            Debug.Log($"[Network][045] RemoteRecoilApplied Pawn={pawn.Root.name} Equipment={equipmentInstanceId} Profile={weapon.RecoilProfile.name} Succeeded={fallbackResult.Succeeded} Sequence={fallbackResult.ShotSequence}");
            return fallbackResult.Succeeded;
        }

        private void AddClip(AnimationClipAsset clip)
        {
            if (clip != null && !clips.ContainsKey(clip.name)) clips.Add(clip.name, clip);
        }

        private void AddMotion(IkMotionLayerSettings motion)
        {
            if (motion != null && !motions.ContainsKey(motion.name)) motions.Add(motion.name, motion);
        }

        private void CreateInitialWeaponPresentation(WeaponDefinition weapon)
        {
            ApplyWeaponPresentation(weapon);
        }

        private void ApplyWeaponPresentation(WeaponDefinition weapon)
        {
            if (weapon.Prefab == null || animation.RigComponent == null) return;
            CharacterAnimInstance instance = animation.AnimInstance;
            if (instance == null) return;
            Transform mount = null;
            foreach (Transform candidate in animation.RigComponent.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "IK WeaponBone") continue;
                mount = candidate;
                break;
            }

            if (mount == null) return;
            if (weaponPresentation != null) UnityEngine.Object.Destroy(weaponPresentation);
            weaponPresentation = UnityEngine.Object.Instantiate(weapon.Prefab, mount);
            weaponPresentation.transform.localPosition = weapon.PresentationLocalPosition;
            weaponPresentation.transform.localRotation = weapon.PresentationLocalRotation;
            weaponPresentation.transform.localScale = weapon.PresentationLocalScale;
            foreach (Renderer renderer in weaponPresentation.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            if (weaponOverlayHandle != null) instance.StopAbilityAnimation(weaponOverlayHandle);
            weaponOverlayHandle = instance.PrepareInitialPose(weapon.OverlayPose);
            if (weapon.ArmedProfile != null) instance.BoneController.LinkProfile(weapon.ArmedProfile);
        }

        private sealed class CompositePlayback
        {
            public CompositePlayback(AnimationPlaybackHandle characterPlayback, RemoteWeaponReloadPlayback weaponReloadPlayback)
            {
                CharacterPlayback = characterPlayback;
                WeaponReloadPlayback = weaponReloadPlayback;
            }

            public AnimationPlaybackHandle CharacterPlayback { get; }
            public RemoteWeaponReloadPlayback WeaponReloadPlayback { get; }
        }

        private sealed class RemoteWeaponReloadPlayback
        {
            private static readonly int ReloadState = Animator.StringToHash("Reload");
            private static readonly int IdleState = Animator.StringToHash("Idle");
            private readonly Animator animator;
            private PlayableGraph graph;
            private bool controllerPlaying;

            private RemoteWeaponReloadPlayback(Animator animator)
            {
                this.animator = animator;
            }

            public static RemoteWeaponReloadPlayback TryStart(GameObject presentation, WeaponDefinition weapon, float elapsedSeconds)
            {
                AnimationClip weaponAnimation = weapon?.ReloadDefinition?.WeaponAnimation;
                Animator animator = presentation != null ? presentation.GetComponentInChildren<Animator>(true) : null;
                if (weaponAnimation == null || animator == null) return null;

                if (weapon.WeaponAnimatorController != null)
                    animator.runtimeAnimatorController = weapon.WeaponAnimatorController;
                animator.enabled = true;
                var playback = new RemoteWeaponReloadPlayback(animator);
                if (animator.runtimeAnimatorController != null && animator.HasState(0, ReloadState))
                {
                    animator.Rebind();
                    float normalizedTime = weaponAnimation.length > 0f ? Mathf.Clamp01(elapsedSeconds / weaponAnimation.length) : 0f;
                    animator.Play(ReloadState, 0, normalizedTime);
                    playback.controllerPlaying = true;
                    return playback;
                }

                playback.graph = PlayableGraph.Create($"{animator.name}.RemoteReloadPresentation");
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(playback.graph, "RemoteReloadPresentation", animator);
                AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(playback.graph, weaponAnimation);
                clipPlayable.SetTime(Mathf.Max(0f, elapsedSeconds));
                output.SetSourcePlayable(clipPlayable);
                playback.graph.Play();
                return playback;
            }

            public void Stop()
            {
                if (controllerPlaying)
                {
                    controllerPlaying = false;
                    if (animator != null && animator.runtimeAnimatorController != null)
                    {
                        animator.Rebind();
                        if (animator.HasState(0, IdleState)) animator.Play(IdleState, 0, 0f);
                    }
                }

                if (graph.IsValid()) graph.Destroy();
            }
        }

    }
}
