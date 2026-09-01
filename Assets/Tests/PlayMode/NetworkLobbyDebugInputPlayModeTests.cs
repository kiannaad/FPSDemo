using System.Collections;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace CGame.Tests.Network
{
    public sealed class NetworkLobbyDebugInputPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator KeyboardSimulation_QueuesLobbyShortcutKeys()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
            InputSystem.Update();
            Assert.That(keyboard.cKey.wasPressedThisFrame, Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            InputSystem.Update();
            Assert.That(keyboard.jKey.wasPressedThisFrame, Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            InputSystem.Update();
            Assert.That(keyboard.rKey.wasPressedThisFrame, Is.True);
            yield return null;
        }
    }
}
