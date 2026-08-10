using System.Collections;
using CGame.InventoryEquipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.InputTag.PlayMode.Tests
{
    public sealed class SampleSceneInputTagAbilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator MouseFire_UsesTheProductionInputTagAbilityChain()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return new WaitUntil(() => load.isDone);

            GameInstance gameInstance = Object.FindFirstObjectByType<GameInstance>();
            Assert.That(gameInstance, Is.Not.Null);
            yield return new WaitUntil(() => gameInstance.InitializationTask.IsCompleted);
            Assert.That(gameInstance.InitializationTask.IsFaulted, Is.False);

            PlayerController controller = gameInstance.RuntimeWorld.GameMode.PlayerController as PlayerController;
            Assert.That(controller, Is.Not.Null);
            EquipmentManagerComponent equipment = controller.PossessedPawn.GetComponent<EquipmentManagerComponent>();
            yield return new WaitUntil(() => equipment.CurrentWeapon != null);
            WeaponInstance weapon = equipment.CurrentWeapon;
            int fireBefore = weapon.FireCount;
            int ammoBefore = weapon.Item.MagazineAmmo;

            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                InputSystem.Update();
                yield return null;
                yield return null;

                Assert.That(weapon.FireCount, Is.EqualTo(fireBefore + 1));
                Assert.That(weapon.Item.MagazineAmmo, Is.EqualTo(ammoBefore - 1));

                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 0 });
                InputSystem.Update();
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }
    }
}
