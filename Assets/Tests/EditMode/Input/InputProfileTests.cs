using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame.Input.Tests
{
    public sealed class InputProfileTests
    {
        private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            foreach (UnityEngine.Object item in objectsToDestroy)
            {
                UnityEngine.Object.DestroyImmediate(item);
            }

            objectsToDestroy.Clear();
        }

        [Test]
        public void Profile_OnlyAcceptsUniqueButtonBindingsFromItsActionMap()
        {
            InitializeTags(Node("InputTag", false, Node("Weapon", false, Node("Fire", true), Node("Reload", true))));
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire");
            GameplayTag reload = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Reload");
            InputActionAsset asset = Track(ScriptableObject.CreateInstance<InputActionAsset>());
            InputActionMap player = asset.AddActionMap("Player");
            InputAction fireAction = player.AddAction("Fire", InputActionType.Button);
            InputAction moveAction = player.AddAction("Move", InputActionType.Value);
            InputTagConfig config = Track(ScriptableObject.CreateInstance<InputTagConfig>());
            InputProfile profile = Track(ScriptableObject.CreateInstance<InputProfile>());

            config.SetBindings(new[] { new InputTagBinding(InputActionReference.Create(fireAction), fire) });
            profile.SetConfiguration(asset, player, config);
            Assert.That(profile.TryValidate(out string validError), Is.True, validError);

            config.SetBindings(new[]
            {
                new InputTagBinding(InputActionReference.Create(fireAction), fire),
                new InputTagBinding(InputActionReference.Create(fireAction), reload)
            });
            Assert.That(profile.TryValidate(out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("cannot repeat"));

            config.SetBindings(new[] { new InputTagBinding(InputActionReference.Create(moveAction), reload) });
            Assert.That(profile.TryValidate(out string typeError), Is.False);
            Assert.That(typeError, Does.Contain("Button"));
        }

        [Test]
        public void InputHandle_ActionReferenceRegistrationDisposesItsCallback()
        {
            var source = new PlayerInput();
            var service = new InputService();
            try
            {
                service.Initialize();
                InputHandle handle = service.GetHandle(InputType.Player);
                int callbackCount = 0;
                using (handle.RegisterActionCallback(
                           InputActionReference.Create(source.Player.Fire),
                           InputCallbackPhase.Performed,
                           _ => callbackCount++))
                {
                    Assert.That(callbackCount, Is.Zero);
                }

                Assert.That(callbackCount, Is.Zero);
            }
            finally
            {
                service.Shutdown();
                UnityEngine.Object.DestroyImmediate(source.asset);
            }
        }

        private T Track<T>(T item) where T : UnityEngine.Object
        {
            objectsToDestroy.Add(item);
            return item;
        }

        private void InitializeTags(params GameplayTagSourceNode[] roots)
        {
            GameplayTagSource source = Track(ScriptableObject.CreateInstance<GameplayTagSource>());
            source.SetDefinition("InputProfileTests", roots);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        }

        private static GameplayTagSourceNode Node(string name, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(name, isExplicit, children: children);
        }
    }
}
