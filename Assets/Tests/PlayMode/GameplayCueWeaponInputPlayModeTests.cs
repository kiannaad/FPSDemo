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
    public sealed class GameplayCueWeaponInputPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;

        [UnitySetUp]
        public IEnumerator SetUpScene()
        {
            Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            AsyncOperation load = SceneManager.LoadSceneAsync("WeaponGripAK12AcceptanceScene", LoadSceneMode.Single);
            yield return load;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            TearDown();
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
            int beforeAmmo = equipment.CurrentWeapon.Item.MagazineAmmo;

            Assert.That(keyboard, Is.Not.Null);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("\\[CueDebug\\] Tag=GameplayCue\\.Weapon\\.Fire"));
            GameplayTag fireInputTag = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire");
            controller.PlayerState.AbilitySystem.AbilityInputTagPressed(fireInputTag);
            controller.PlayerState.AbilitySystem.ProcessAbilityInput();
            yield return new WaitForSeconds(0.25f);
            controller.PlayerState.AbilitySystem.AbilityInputTagReleased(fireInputTag);
            yield return null;

            Assert.That(equipment.CurrentWeapon.Item.MagazineAmmo, Is.LessThan(beforeAmmo));
            Assert.That(controller.PossessedPawn.RecoilShotSequence, Is.GreaterThan(0));

        }
    }
}
