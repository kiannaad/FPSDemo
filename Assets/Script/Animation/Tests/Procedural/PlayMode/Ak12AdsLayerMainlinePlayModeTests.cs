using System;
using System.Collections;
using CGame.Animation.Rig;
using CGame.InventoryEquipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.Animation.Tests
{
    public sealed class Ak12AdsLayerMainlinePlayModeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_Ak12AdsLayerConsumesAimAbilityAndWritesCameraNode()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return new WaitUntil(() => load.isDone);
            yield return WaitForWorldPlaying();

            GameInstance game = UnityEngine.Object.FindObjectOfType<GameInstance>();
            PlayerController controller = game.RuntimeWorld.GameMode.PlayerController as PlayerController;
            Assert.That(controller, Is.Not.Null);
            Pawn pawn = controller.PossessedPawn;
            EquipmentManagerComponent equipment = pawn.GetComponent<EquipmentManagerComponent>();
            PawnAnimationComponent animation = pawn.GetComponent<PawnAnimationComponent>();
            Assert.That(equipment, Is.Not.Null);
            Assert.That(animation?.AnimInstance, Is.Not.Null);

            yield return WaitForWeapon(equipment);
            int slot = FindWeaponSlot(controller.QuickBar, "AK12WeaponDefinition");
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(controller.QuickBar.SelectSlot(slot), Is.True);
            yield return WaitForWeaponDefinition(equipment, "AK12WeaponDefinition");

            BoneProfile profile = equipment.CurrentWeapon.Definition.ArmedProfile;
            CharacterBoneController boneController = animation.AnimInstance.BoneController;
            yield return WaitForActiveProfile(boneController, profile);
            Assert.That(profile.Layers.Count, Is.EqualTo(8));
            Assert.That(profile.Layers[3], Is.TypeOf<AdsLayerSettings>());
            Assert.That(((AdditiveLayerSettings)profile.Layers[4]).AdsScalar, Is.EqualTo(0.3f));
            WeaponBoneProfileValidator.ValidateAk12(profile);
            Camera presentationCamera = pawn.Root.GetComponentInChildren<Camera>(true);
            Assert.That(presentationCamera, Is.Not.Null);
            float idleFov = presentationCamera.fieldOfView;

            Mouse mouse = InputSystem.AddDevice<Mouse>("Ak12AdsLayerMainlineMouse");
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                InputSystem.Update();
                for (int frame = 0; frame < 45; frame++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                    InputSystem.Update();
                    World.Current.UpdateTick(1f / 60f);
                    yield return null;
                }

                Assert.That(pawn.IsAiming, Is.True, "Held right-button Aim must activate after the transient equip/switch block clears.");
                AdsJob beforeRelease = GetAdsJob(animation.Animator.playableGraph);
                Assert.That(beforeRelease.Weight, Is.GreaterThan(0.99f));
                Assert.That(beforeRelease.CameraBlend, Is.EqualTo(1f));
                Assert.That(beforeRelease.AimingWeight, Is.GreaterThan(0f));
                Assert.That(GetAdditiveJob(animation.Animator.playableGraph).CurveScale, Is.EqualTo(0.3f).Within(0.01f));
                Assert.That(presentationCamera.fieldOfView, Is.LessThan(idleFov));
                AdsLayerSettings adsSettings = (AdsLayerSettings)profile.Layers[3];
                Transform animationCamera = RigHandleUtility.ResolveTransform(
                    animation.RigComponent,
                    adsSettings.AimTargetBone,
                    nameof(Ak12AdsLayerMainlinePlayModeTests));
                Assert.That(Vector3.Distance(presentationCamera.transform.position, animationCamera.position), Is.LessThan(0.001f));

                for (int frame = 0; frame < 40; frame++) yield return null;
                Assert.That(GetAdditiveJob(animation.Animator.playableGraph).CurveScale, Is.EqualTo(0.3f).Within(0.01f));

                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                World.Current.UpdateTick(1f / 60f);
                yield return null;
                Assert.That(pawn.IsAiming, Is.False);
                Assert.That(GetAdsJob(animation.Animator.playableGraph).AimingWeight, Is.LessThan(1f));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        private static IEnumerator WaitForWorldPlaying()
        {
            for (int frame = 0; frame < 300; frame++)
            {
                GameInstance game = UnityEngine.Object.FindObjectOfType<GameInstance>();
                if (game != null && game.InitializationTask?.IsCompleted == true
                    && !game.InitializationTask.IsFaulted && game.RuntimeWorld?.State == WorldState.Playing) yield break;
                yield return null;
            }
            throw new TimeoutException("SampleScene did not create a playing World.");
        }

        private static IEnumerator WaitForWeapon(EquipmentManagerComponent equipment)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (equipment.CurrentWeapon != null) yield break;
                yield return null;
            }
            throw new TimeoutException("SampleScene did not arm its initial weapon.");
        }

        private static int FindWeaponSlot(IQuickBarComponent quickBar, string definitionName)
        {
            for (int index = 0; index < quickBar.Slots.Count; index++)
            {
                if (quickBar.Inventory.TryGet(quickBar.Slots[index], out ItemInstance item)
                    && item.Definition is WeaponItemDefinition weaponItem
                    && weaponItem.WeaponDefinition?.name == definitionName) return index;
            }
            return -1;
        }

        private static IEnumerator WaitForWeaponDefinition(EquipmentManagerComponent equipment, string definitionName)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (equipment.CurrentWeapon?.Definition.name == definitionName) yield break;
                yield return null;
            }
            throw new TimeoutException("SampleScene did not equip AK12 through QuickBar.");
        }

        private static IEnumerator WaitForActiveProfile(CharacterBoneController controller, BoneProfile profile)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (controller.ActiveProfile == profile) yield break;
                yield return null;
            }
            throw new TimeoutException("AK12 BoneProfile was not linked through the equipment mainline.");
        }

        private static AdsJob GetAdsJob(PlayableGraph graph)
        {
            for (int outputIndex = 0; outputIndex < graph.GetOutputCount(); outputIndex++)
            {
                PlayableOutput output = graph.GetOutput(outputIndex);
                if (!output.IsOutputValid() || output.GetPlayableOutputType() != typeof(AnimationPlayableOutput)) continue;
                AnimationPlayableOutput animationOutput = (AnimationPlayableOutput)output;
                if (animationOutput.GetAnimationStreamSource() != AnimationStreamSource.PreviousInputs) continue;
                AnimationScriptPlayable ik = (AnimationScriptPlayable)((AnimationScriptPlayable)animationOutput.GetSourcePlayable()).GetInput(0);
                AnimationScriptPlayable turn = (AnimationScriptPlayable)ik.GetInput(0);
                AnimationScriptPlayable look = (AnimationScriptPlayable)turn.GetInput(0);
                AnimationScriptPlayable additive = (AnimationScriptPlayable)look.GetInput(0);
                AnimationScriptPlayable ads = (AnimationScriptPlayable)additive.GetInput(0);
                return ads.GetJobData<AdsJob>();
            }
            Assert.Fail("Missing CharacterBoneController PreviousInputs output.");
            return default;
        }

        private static AdditiveJob GetAdditiveJob(PlayableGraph graph)
        {
            for (int outputIndex = 0; outputIndex < graph.GetOutputCount(); outputIndex++)
            {
                PlayableOutput output = graph.GetOutput(outputIndex);
                if (!output.IsOutputValid() || output.GetPlayableOutputType() != typeof(AnimationPlayableOutput)) continue;
                AnimationPlayableOutput animationOutput = (AnimationPlayableOutput)output;
                if (animationOutput.GetAnimationStreamSource() != AnimationStreamSource.PreviousInputs) continue;
                AnimationScriptPlayable ik = (AnimationScriptPlayable)((AnimationScriptPlayable)animationOutput.GetSourcePlayable()).GetInput(0);
                AnimationScriptPlayable turn = (AnimationScriptPlayable)ik.GetInput(0);
                AnimationScriptPlayable look = (AnimationScriptPlayable)turn.GetInput(0);
                AnimationScriptPlayable additive = (AnimationScriptPlayable)look.GetInput(0);
                return additive.GetJobData<AdditiveJob>();
            }
            Assert.Fail("Missing CharacterBoneController PreviousInputs output.");
            return default;
        }

    }
}
