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
    public sealed class Ak12PoseSamplerIkMainlinePlayModeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_EquipsAk12AndRunsThePromotedSixLayerBoneProfile()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return new WaitUntil(() => load.isDone);
            yield return WaitForWorldPlaying();

            GameInstance gameInstance = UnityEngine.Object.FindObjectOfType<GameInstance>();
            PlayerController controller = gameInstance.RuntimeWorld.GameMode.PlayerController as PlayerController;
            Assert.That(controller, Is.Not.Null);
            Pawn pawn = controller.PossessedPawn;
            Assert.That(pawn, Is.Not.Null);

            EquipmentManagerComponent equipment = pawn.GetComponent<EquipmentManagerComponent>();
            PawnAnimationComponent animation = pawn.GetComponent<PawnAnimationComponent>();
            Assert.That(equipment, Is.Not.Null);
            Assert.That(animation?.AnimInstance, Is.Not.Null);
            yield return WaitForWeapon(equipment);

            int ak12Slot = FindWeaponSlot(controller.QuickBar, "AK12WeaponDefinition");
            Assert.That(ak12Slot, Is.GreaterThanOrEqualTo(0), "SampleScene formal QuickBar must contain AK12.");
            Assert.That(controller.QuickBar.SelectSlot(ak12Slot), Is.True);
            yield return WaitForWeaponDefinition(equipment, "AK12WeaponDefinition");

            WeaponInstance weapon = equipment.CurrentWeapon;
            BoneProfile profile = weapon.Definition.ArmedProfile;
            CharacterBoneController boneController = animation.AnimInstance.BoneController;
            yield return WaitForActiveProfile(boneController, profile);

            Assert.That(profile.name, Is.EqualTo("AK12ProceduralBoneProfile"));
            Assert.That(profile.Layers.Count, Is.EqualTo(6));
            Assert.That(profile.Layers[0], Is.TypeOf<PoseSamplerLayerSettings>());
            Assert.That(profile.Layers[1], Is.TypeOf<AttachHandLayerSettings>());
            Assert.That(profile.Layers[2], Is.TypeOf<ViewLayerSettings>());
            Assert.That(profile.Layers[3], Is.TypeOf<LookLayerSettings>());
            Assert.That(profile.Layers[4], Is.TypeOf<TurnLayerSettings>());
            Assert.That(profile.Layers[5], Is.TypeOf<IkLayerSettings>());
            Assert.That(() => WeaponBoneProfileValidator.ValidateAk12(profile), Throws.Nothing);
            Assert.That(weapon.IsArmed, Is.True);
            Assert.That(weapon.PresentationRoot, Is.Not.Null);
            Assert.That(
                weapon.PresentationRoot.GetComponentsInChildren<Renderer>(true),
                Has.All.Matches<Renderer>(renderer => renderer.enabled));

            KRigComponent rig = animation.RigComponent;
            var samplerSettings = (PoseSamplerLayerSettings)profile.Layers[0];
            Assert.That(
                weapon.PresentationRoot.transform.parent,
                Is.SameAs(RigHandleUtility.ResolveTransform(
                    rig,
                    samplerSettings.IkWeaponBone,
                    nameof(Ak12PoseSamplerIkMainlinePlayModeTests))));

            AnimationScriptPlayable ikPlayable = GetFinalProfilePlayable(animation.Animator.playableGraph);
            AnimationScriptPlayable turnPlayable = (AnimationScriptPlayable)ikPlayable.GetInput(0);
            AnimationScriptPlayable lookPlayable = (AnimationScriptPlayable)turnPlayable.GetInput(0);
            AnimationScriptPlayable viewPlayable = (AnimationScriptPlayable)lookPlayable.GetInput(0);
            AnimationScriptPlayable attachPlayable = (AnimationScriptPlayable)viewPlayable.GetInput(0);
            AnimationScriptPlayable samplerPlayable = (AnimationScriptPlayable)attachPlayable.GetInput(0);
            Assert.That(ikPlayable.GetJobData<IkJob>().Weight, Is.GreaterThan(0.99f));
            PoseSamplerJob samplerJob = samplerPlayable.GetJobData<PoseSamplerJob>();
            Assert.That(samplerJob.Weight, Is.GreaterThan(0.99f));
            Assert.That(
                samplerJob.WeaponBoneWeight,
                Is.Zero,
                "Without an active slot animation NamedCurve, WeaponBoneWeight must default to zero.");

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Ak12PoseSamplerIkKeyboard");
            Mouse mouse = InputSystem.AddDevice<Mouse>("Ak12ProceduralPromotionMouse");
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(24f, 0f) });
                InputSystem.Update();
                World.Current.UpdateTick(1f / 60f);
                for (int frame = 0; frame < 12; frame++) yield return null;

                Assert.That(boneController.ActiveProfile, Is.SameAs(profile));
                Assert.That(equipment.CurrentWeapon, Is.SameAs(weapon));
                Assert.That(animation.Animator.playableGraph.IsValid(), Is.True);
                Assert.That(controller.ControlYaw, Is.GreaterThan(0f),
                    "Formal SampleScene Mouse input must reach PlayerController while promoted AK12 is armed.");

                Transform weaponBoneRight = RigHandleUtility.ResolveTransform(
                    rig,
                    samplerSettings.WeaponBoneRight,
                    nameof(Ak12PoseSamplerIkMainlinePlayModeTests));
                Transform ikWeaponBone = RigHandleUtility.ResolveTransform(
                    rig,
                    samplerSettings.IkWeaponBone,
                    nameof(Ak12PoseSamplerIkMainlinePlayModeTests));
                Assert.That(
                    Vector3.Distance(
                        weaponBoneRight.localPosition,
                        samplerJob.WeaponBoneRightLocalPose.Position),
                    Is.LessThan(0.0001f),
                    "The right-hand weapon reference must retain its sampled local grip offset while the hand animates.");
                Assert.That(
                    Quaternion.Angle(
                        weaponBoneRight.localRotation,
                        samplerJob.WeaponBoneRightLocalPose.Rotation),
                    Is.LessThan(0.05f),
                    "The right-hand weapon reference must retain its sampled local grip rotation while the hand animates.");
                Assert.That(Vector3.Distance(ikWeaponBone.position, weaponBoneRight.position), Is.LessThan(0.001f),
                    "WeaponBoneWeight zero must make IK WeaponBone follow the animated right-hand reference.");
                Assert.That(Quaternion.Angle(ikWeaponBone.rotation, weaponBoneRight.rotation), Is.LessThan(0.1f),
                    "IK WeaponBone must preserve the animated right-hand reference rotation.");
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static IEnumerator WaitForWorldPlaying()
        {
            for (int frame = 0; frame < 300; frame++)
            {
                GameInstance gameInstance = UnityEngine.Object.FindObjectOfType<GameInstance>();
                if (gameInstance != null
                    && gameInstance.InitializationTask != null
                    && gameInstance.InitializationTask.IsCompleted
                    && !gameInstance.InitializationTask.IsFaulted
                    && gameInstance.RuntimeWorld?.State == WorldState.Playing)
                {
                    yield break;
                }

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
                if (!quickBar.Inventory.TryGet(quickBar.Slots[index], out ItemInstance item)) continue;
                if (item.Definition is WeaponItemDefinition weaponItem
                    && weaponItem.WeaponDefinition != null
                    && weaponItem.WeaponDefinition.name == definitionName)
                {
                    return index;
                }
            }
            return -1;
        }

        private static IEnumerator WaitForWeaponDefinition(EquipmentManagerComponent equipment, string definitionName)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (equipment.CurrentWeapon != null && equipment.CurrentWeapon.Definition.name == definitionName) yield break;
                yield return null;
            }
            throw new TimeoutException($"SampleScene did not equip {definitionName} through QuickBar.");
        }

        private static IEnumerator WaitForActiveProfile(
            CharacterBoneController controller,
            BoneProfile profile)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (controller.ActiveProfile == profile) yield break;
                yield return null;
            }

            throw new TimeoutException("AK12 BoneProfile was not linked through the equipment mainline.");
        }

        private static AnimationScriptPlayable GetFinalProfilePlayable(PlayableGraph graph)
        {
            for (int index = 0; index < graph.GetOutputCount(); index++)
            {
                PlayableOutput output = graph.GetOutput(index);
                if (!output.IsOutputValid()
                    || output.GetPlayableOutputType() != typeof(AnimationPlayableOutput))
                {
                    continue;
                }

                AnimationPlayableOutput animationOutput = (AnimationPlayableOutput)output;
                if (animationOutput.GetAnimationStreamSource() != AnimationStreamSource.PreviousInputs)
                {
                    continue;
                }

                AnimationScriptPlayable blendingPlayable =
                    (AnimationScriptPlayable)animationOutput.GetSourcePlayable();
                return (AnimationScriptPlayable)blendingPlayable.GetInput(0);
            }

            Assert.Fail("Missing CharacterBoneController PreviousInputs output.");
            return AnimationScriptPlayable.Null;
        }
    }
}
