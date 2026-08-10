using System;
using System.Collections;
using CGame.Ability;
using CGame.GameplayTags;
using CGame.InventoryEquipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace CGame.InputTag.PlayMode.Tests
{
    public sealed class PawnHeroLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator PossessAndUnpossess_BindsAndReleasesPawnHero()
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("PawnHeroLifecycle", new[]
            {
                new GameplayTagSourceNode("InputTag", false, children: new[]
                {
                    new GameplayTagSourceNode("Weapon", false, children: new[] { new GameplayTagSourceNode("Fire", true) })
                })
            });
            Assert.That(GameplayTagManager.Instance.Initialize(new[] { source }).Succeeded, Is.True);
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire");
            var generatedInput = new PlayerInput();
            InputTagConfig config = ScriptableObject.CreateInstance<InputTagConfig>();
            config.SetBindings(new[] { new InputTagBinding(InputActionReference.Create(generatedInput.Player.Fire), fire) });
            InputProfile profile = ScriptableObject.CreateInstance<InputProfile>();
            profile.SetConfiguration(generatedInput.asset, generatedInput.Player.Get(), config);
            var inputService = new InputService();
            inputService.Initialize();
            World world = World.Create();
            try
            {
                world.InitializeAsync().GetAwaiter().GetResult();
                var ability = new InputAbilityDefinition(GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire"));
                var definition = PawnDefinition.CreateRuntime(null, new AbilitySet(new[] { new AbilityGrantDefinition(ability, fire) }));
                definition.SetInputProfile(profile);
                var controller = PlayerController.Create(world.LocalPlayer, definition, null, new TestInputSource(inputService.GetHandle(InputType.Player)), new DefaultPlayerControllerComponentFactory());
                world.RegisterActor(controller);
                var pawn = new Pawn(null, new ActorComponent[] { new PawnHeroComponent(profile) });
                world.RegisterActor(pawn);

                controller.Possess(pawn);
                PawnHeroComponent hero = pawn.GetComponent<PawnHeroComponent>();
                Assert.That(hero.IsBound, Is.True);
                Assert.That(controller.PlayerState.Avatar, Is.SameAs(pawn));

                Mouse mouse = InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                InputSystem.Update();
                controller.UpdatingController(0.016f);
                Assert.That(ability.Instance.ActivateCount, Is.EqualTo(1));

                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 0 });
                InputSystem.Update();
                controller.UpdatingController(0.016f);
                Assert.That(ability.Instance.ReleaseCount, Is.EqualTo(1));
                InputSystem.RemoveDevice(mouse);

                controller.Unpossess();
                Assert.That(hero.IsBound, Is.False);
                Assert.That(controller.PlayerState.Avatar, Is.Null);
            }
            finally
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
                inputService.Shutdown();
                UnityEngine.Object.DestroyImmediate(generatedInput.asset);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(source);
                GameplayTagManager.Instance.Shutdown();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator QuickBarReplacement_ReplacesSharedInputTagAndRetainsOldEquipmentOnLoadFailure()
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("EquipmentReplacement", new[]
            {
                new GameplayTagSourceNode("InputTag", false, children: new[]
                {
                    new GameplayTagSourceNode("Weapon", false, children: new[] { new GameplayTagSourceNode("Fire", true) })
                })
            });
            Assert.That(GameplayTagManager.Instance.Initialize(new[] { source }).Succeeded, Is.True);
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire");
            var abilitySet = new AbilitySet(new[] { new AbilityGrantDefinition(new InputAbilityDefinition(fire), fire) });
            WeaponEquipmentDefinition firstEquipment = WeaponEquipmentDefinition.CreateRuntime(30, abilitySets: abilitySet);
            WeaponEquipmentDefinition secondEquipment = WeaponEquipmentDefinition.CreateRuntime(30, abilitySets: abilitySet);
            WeaponEquipmentDefinition failingEquipment = WeaponEquipmentDefinition.CreateRuntime(30, simulateLoadFailure: true, abilitySets: abilitySet);
            WeaponItemDefinition firstItem = WeaponItemDefinition.CreateRuntime(firstEquipment, 10, 20);
            WeaponItemDefinition secondItem = WeaponItemDefinition.CreateRuntime(secondEquipment, 10, 20);
            WeaponItemDefinition failingItem = WeaponItemDefinition.CreateRuntime(failingEquipment, 10, 20);
            InitialInventorySet inventory = InitialInventorySet.CreateRuntime(0, firstItem, secondItem, failingItem);
            World world = World.Create();
            try
            {
                world.InitializeAsync().GetAwaiter().GetResult();
                PawnDefinition pawnDefinition = PawnDefinition.CreateRuntime(null, Array.Empty<AbilitySet>());
                PlayerController controller = PlayerController.Create(world.LocalPlayer, pawnDefinition, inventory, new TestInputSource(null), new DefaultPlayerControllerComponentFactory());
                world.RegisterActor(controller);
                var pawn = new Pawn(null, new ActorComponent[] { new EquipmentManagerComponent() });
                world.RegisterActor(pawn);
                controller.Possess(pawn);
                world.StartPlay();
                EquipmentManagerComponent equipment = pawn.GetComponent<EquipmentManagerComponent>();

                controller.QuickBar.SelectSlot(0);
                world.UpdateTick(0.016f);
                world.UpdateTick(0.016f);
                EquipmentInstance first = equipment.CurrentEquipment;
                Assert.That(first, Is.Not.Null);

                controller.QuickBar.SelectSlot(1);
                world.UpdateTick(0.016f);
                world.UpdateTick(0.016f);
                EquipmentInstance second = equipment.CurrentEquipment;
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(first.IsDisposed, Is.True);

                controller.QuickBar.SelectSlot(2);
                world.UpdateTick(0.016f);
                world.UpdateTick(0.016f);
                Assert.That(equipment.CurrentEquipment, Is.SameAs(second));
                Assert.That(second.IsDisposed, Is.False);
            }
            finally
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(inventory);
                UnityEngine.Object.DestroyImmediate(firstItem);
                UnityEngine.Object.DestroyImmediate(secondItem);
                UnityEngine.Object.DestroyImmediate(failingItem);
                UnityEngine.Object.DestroyImmediate(firstEquipment);
                UnityEngine.Object.DestroyImmediate(secondEquipment);
                UnityEngine.Object.DestroyImmediate(failingEquipment);
                UnityEngine.Object.DestroyImmediate(source);
                GameplayTagManager.Instance.Shutdown();
            }

            yield return null;
        }

        private sealed class TestInputSource : IPlayerInputSource
        {
            public TestInputSource(InputHandle inputHandle) { InputHandle = inputHandle; }
            public InputHandle InputHandle { get; }
            public CharacterControlIntent ReadControlIntent() => default;
            public Vector2 ReadLookDelta(float deltaTime) => default;
            public bool FirePressed => false;
            public bool ReloadPressed => false;
            public bool MeleePressed => false;
            public int RequestedQuickBarSlot => -1;
        }

        private sealed class InputAbilityDefinition : AbilityDefinition
        {
            public InputAbilityDefinition(GameplayTag abilityTag) : base(abilityTag) { }
            public InputAbilityInstance Instance { get; private set; }
            protected override AbilityInstance CreateInstance() => Instance = new InputAbilityInstance();
        }

        private sealed class InputAbilityInstance : AbilityInstance
        {
            public int ActivateCount { get; private set; }
            public int ReleaseCount { get; private set; }
            protected override void OnActivate() => ActivateCount++;
            protected override void OnInputReleased() => ReleaseCount++;
        }
    }
}
