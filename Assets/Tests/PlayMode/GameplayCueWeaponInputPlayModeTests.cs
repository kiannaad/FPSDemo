using System.Collections;
using CGame;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class GameplayCueWeaponInputPlayModeTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private OfflineSampleSceneFixture offlineFixture;

        [UnitySetUp]
        public IEnumerator SetUpScene()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            offlineFixture = new OfflineSampleSceneFixture();
            AsyncOperation load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return load;
            yield return offlineFixture.Start(Object.FindObjectOfType<GameInstance>());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            offlineFixture?.Dispose();
            offlineFixture = null;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            yield return null;
        }

        [UnityTest]
        public IEnumerator QueueStateEvent_FireInput_ConsumesAmmoAndDispatchesWeaponCue()
        {
            yield return new WaitUntil(() => World.Current != null && World.Current.State == WorldState.Playing);
            PlayerController controller = World.Current.GameMode.PlayerController as PlayerController;
            Assert.That(controller, Is.Not.Null);
            yield return new WaitUntil(() =>
            {
                EquipmentManagerComponent pendingEquipment = null;
                return controller.PossessedPawn != null &&
                       controller.PossessedPawn.TryGetComponent(out pendingEquipment) &&
                       pendingEquipment.CurrentWeapon != null &&
                       pendingEquipment.CurrentWeapon.IsArmed;
            });
            EquipmentManagerComponent equipment = null;
            Assert.That(controller.PossessedPawn.TryGetComponent(out equipment), Is.True);
            Assert.That(equipment.CurrentWeapon, Is.Not.Null);
            int firearmSlot = -1;
            for (int index = 0; index < controller.QuickBar.Slots.Count; index++)
            {
                if (controller.Inventory.TryGet(controller.QuickBar.Slots[index], out ItemInstance item) &&
                    item.Definition is CGame.InventoryEquipment.WeaponItemDefinition weaponItem &&
                    weaponItem.WeaponDefinition != null && weaponItem.WeaponDefinition.DamageEffect != null)
                {
                    firearmSlot = index;
                    break;
                }
            }
            Assert.That(firearmSlot, Is.GreaterThanOrEqualTo(0), "The fixture requires a firearm, not the default knife.");
            Assert.That(controller.TryRequestQuickBarSlot(firearmSlot), Is.True);
            for (int frame = 0; frame < 300 && (equipment.IsSwitchInProgress ||
                 equipment.CurrentWeapon.ItemHandle != controller.QuickBar.Slots[firearmSlot] ||
                 !equipment.CurrentWeapon.IsArmed); frame++) yield return null;
            Assert.That(equipment.CurrentWeapon.ItemHandle, Is.EqualTo(controller.QuickBar.Slots[firearmSlot]));
            Assert.That(equipment.IsSwitchInProgress, Is.False);
            Assert.That(equipment.CurrentWeapon.IsArmed, Is.True);
            int beforeAmmo = equipment.CurrentWeapon.Item.MagazineAmmo;

            Assert.That(keyboard, Is.Not.Null);
            int fireCueCount = 0;
            World.Current.GetSubSystem<GameplayCueManager>().RegisterRoute(
                GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.Fire"), (_, _, _) => fireCueCount++);
            Assert.That(mouse.added, Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return new WaitForSeconds(0.25f);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;

            Assert.That(fireCueCount, Is.GreaterThan(0),
                $"Fire Cue route: focused={Application.isFocused}, ammo={beforeAmmo}->{equipment.CurrentWeapon.Item.MagazineAmmo}, recoil={controller.PossessedPawn.RecoilShotSequence}, mouseCurrent={Mouse.current == mouse}.");
            Assert.That(equipment.CurrentWeapon.Item.MagazineAmmo, Is.LessThan(beforeAmmo));
            Assert.That(controller.PossessedPawn.RecoilShotSequence, Is.GreaterThan(0));

        }
    }
}
