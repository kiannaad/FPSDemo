using System;
using System.Collections;
using System.IO;
using CGame.Animation;
using CGame.Animation.Rig;
using CGame.InventoryEquipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.Animation.Tests
{
    public sealed class Ak12AdsInteractiveMainlinePlayModeTests
    {
        private const string HeldCapture = "E:/UnityProgram/FPS/.harness/runs/AK12AdsMainline-006-20260818-220625/visual/ads-sample-held.png";
        private const string ReleasedCapture = "E:/UnityProgram/FPS/.harness/runs/AK12AdsMainline-006-20260818-220625/visual/ads-sample-released.png";

        [UnityTest]
        public IEnumerator SampleScene_InputSystemDrivesAk12AdsFireReleaseAndWeaponChangeCancellation()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return WaitForWorld();
            GameInstance game = UnityEngine.Object.FindObjectOfType<GameInstance>();
            PlayerController controller = game.RuntimeWorld.GameMode.PlayerController as PlayerController;
            Pawn pawn = controller.PossessedPawn;
            EquipmentManagerComponent equipment = pawn.GetComponent<EquipmentManagerComponent>();
            PawnAnimationComponent animation = pawn.GetComponent<PawnAnimationComponent>();
            yield return WaitForWeapon(equipment);

            int ak12Slot = FindSlot(controller.QuickBar, "AK12WeaponDefinition");
            Assert.That(controller.QuickBar.SelectSlot(ak12Slot), Is.True, "AK12 quick-bar selection must succeed.");
            yield return WaitForDefinition(equipment, "AK12WeaponDefinition");
            WeaponInstance weapon = equipment.CurrentWeapon;
            Camera camera = pawn.Root.GetComponentInChildren<Camera>(true);
            float idleFov = camera.fieldOfView;
            Assert.That(pawn.CurrentWeaponAimPoint, Is.Not.Null,
                "Equipping AK12 must bind its authored WeaponAimPoint to the Pawn.");
            Vector3 expectedAimOffset = weapon.PresentationRoot.transform.InverseTransformPoint(
                pawn.CurrentWeaponAimPoint.position);
            Assert.That(Vector3.Distance(pawn.AimPointOffset.position, -expectedAimOffset), Is.LessThan(0.0001f));
            Mouse mouse = InputSystem.AddDevice<Mouse>("Ak12AdsInteractiveMouse");
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                InputSystem.Update();
                for (int frame = 0; frame < 45; frame++)
                {
                    World.Current.UpdateTick(1f / 60f);
                    yield return null;
                }
                Assert.That(pawn.IsAiming, Is.True, "Right-button Aim input must activate the production aim ability.");
                Assert.That(camera.fieldOfView, Is.LessThan(idleFov));
                BoneProfile profile = animation.AnimInstance.BoneController.ActiveProfile;
                AdsLayerSettings adsSettings = (AdsLayerSettings)profile.Layers[3];
                Transform animationCamera = RigHandleUtility.ResolveTransform(
                    animation.RigComponent,
                    adsSettings.AimTargetBone,
                    nameof(Ak12AdsInteractiveMainlinePlayModeTests));
                Assert.That(Vector3.Distance(camera.transform.position, animationCamera.position), Is.LessThan(0.001f),
                    "Full ADS must consume the production animation Camera node without cumulative drift.");
                ScreenCapture.CaptureScreenshot(HeldCapture);
                yield return new WaitForEndOfFrame();

                int fireCount = weapon.FireCount;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                InputSystem.Update();
                World.Current.UpdateTick(1f / 60f);
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(weapon.FireCount, Is.GreaterThan(fireCount));
                Assert.That(pawn.RecoilRotationOffsetDegrees.sqrMagnitude, Is.GreaterThan(0f));
                Assert.That(pawn.RecoilShotSequence, Is.GreaterThan(0));

                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                for (int frame = 0; frame < 45; frame++)
                {
                    World.Current.UpdateTick(1f / 60f);
                    yield return null;
                }
                Assert.That(pawn.IsAiming, Is.False);
                ScreenCapture.CaptureScreenshot(ReleasedCapture);
                yield return new WaitForEndOfFrame();

                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                InputSystem.Update();
                World.Current.UpdateTick(1f / 60f);
                yield return null;
                Assert.That(pawn.IsAiming, Is.True);
                Assert.That(controller.QuickBar.SelectSlot(0), Is.True);
                for (int frame = 0; frame < 300 && ReferenceEquals(equipment.CurrentWeapon, weapon); frame++) yield return null;
                Assert.That(equipment.CurrentWeapon, Is.Not.SameAs(weapon));
                Assert.That(pawn.IsAiming, Is.False);
                Assert.That(File.Exists(HeldCapture), Is.True);
                Assert.That(File.Exists(ReleasedCapture), Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        private static IEnumerator WaitForWorld()
        {
            for (int frame = 0; frame < 300; frame++)
            {
                GameInstance game = UnityEngine.Object.FindObjectOfType<GameInstance>();
                if (game != null && game.InitializationTask?.IsCompleted == true && !game.InitializationTask.IsFaulted && game.RuntimeWorld?.State == WorldState.Playing) yield break;
                yield return null;
            }
            throw new TimeoutException("SampleScene did not reach World.Playing.");
        }

        private static IEnumerator WaitForWeapon(EquipmentManagerComponent equipment)
        {
            for (int frame = 0; frame < 300; frame++) { if (equipment.CurrentWeapon != null) yield break; yield return null; }
            throw new TimeoutException("SampleScene did not arm an initial weapon.");
        }

        private static IEnumerator WaitForDefinition(EquipmentManagerComponent equipment, string name)
        {
            for (int frame = 0; frame < 300; frame++) { if (equipment.CurrentWeapon?.Definition.name == name) yield break; yield return null; }
            throw new TimeoutException("SampleScene did not equip " + name + ".");
        }

        private static int FindSlot(IQuickBarComponent quickBar, string definitionName)
        {
            for (int index = 0; index < quickBar.Slots.Count; index++)
                if (quickBar.Inventory.TryGet(quickBar.Slots[index], out ItemInstance item)
                    && item.Definition is WeaponItemDefinition weaponItem
                    && weaponItem.WeaponDefinition?.name == definitionName) return index;
            return -1;
        }
    }
}
