using System.Collections;
using System.Reflection;
using CGame.Ability;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Gameplay.PlayMode.Tests
{
    public sealed class PlayerGameModeControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator ProductionBootstrap_OwnsCursorGameModeAndControllerLifecycle()
        {
            CursorLockMode originalLockMode = Cursor.lockState;
            bool originalVisible = Cursor.visible;
            GameBootstrap bootstrap = ScriptableObject.CreateInstance<GameBootstrap>();
            DefaultGameModeDefinition gameModeDefinition =
                ScriptableObject.CreateInstance<DefaultGameModeDefinition>();
            PawnData pawnData = PawnData.CreateRuntime(new AbilitySet());
            GameObject pawnPrefab = new GameObject("BootstrapPawnPrefab");
            pawnPrefab.SetActive(false);
            Object.Destroy(pawnData);
            pawnData = PawnData.CreateRuntime(pawnPrefab, new AbilitySet());

            SetField(bootstrap, "initializeResources", false);
            SetField(bootstrap, "initializeInput", false);
            SetField(bootstrap, "gameModeDefinition", gameModeDefinition);
            SetField(gameModeDefinition, "pawnDefinition", pawnData);

            World world = World.Create(bootstrap);
            yield return Await(world.InitializeAsync());

            Assert.That(Cursor.lockState, Is.EqualTo(originalLockMode));
            Assert.That(Cursor.visible, Is.EqualTo(originalVisible));
            Assert.That(world.LocalPlayer.GetSubSystem<CursorSubSystem>(), Is.Not.Null);
            Assert.That(world.GameMode, Is.TypeOf<DefaultGameMode>());
            Assert.That(world.LocalPlayer.Controller, Is.TypeOf<PlayerController>());

            world.StartPlay();
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(Cursor.visible, Is.False);

            yield return Await(world.ShutdownAsync());
            Assert.That(Cursor.lockState, Is.EqualTo(originalLockMode));
            Assert.That(Cursor.visible, Is.EqualTo(originalVisible));
            Assert.That(World.Current, Is.Null);

            Object.Destroy(pawnData);
            Object.Destroy(pawnPrefab);
            Object.Destroy(gameModeDefinition);
            Object.Destroy(bootstrap);
            yield return null;
        }

        private static IEnumerator Await(System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception.InnerException;
            }
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {name} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
