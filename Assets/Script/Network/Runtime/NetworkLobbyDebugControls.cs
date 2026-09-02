using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame.Network
{
    [DisallowMultipleComponent]
    public sealed class NetworkLobbyDebugControls : MonoBehaviour
    {
        [SerializeField] private string roomIdToJoin = string.Empty;
        private string status = "Waiting for network game mode";
        private bool createWasPressed;
        private bool joinWasPressed;
        private bool readyWasPressed;

        private void Update()
        {
            if (!Application.isFocused) return;
            if (!(GetComponent<GameInstance>()?.RuntimeWorld?.GameMode is INetworkLobby lobby))
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool createIsPressed = keyboard.cKey.isPressed;
            bool joinIsPressed = keyboard.jKey.isPressed;
            bool readyIsPressed = keyboard.rKey.isPressed;

            if (createIsPressed && !createWasPressed)
            {
                Run(lobby.CreateRoomAsync);
            }
            else if (joinIsPressed && !joinWasPressed)
            {
                Run(() => lobby.JoinRoomAsync(GUIUtility.systemCopyBuffer.Trim()));
            }
            else if (readyIsPressed && !readyWasPressed)
            {
                Run(() => lobby.SetReadyAsync(true));
            }

            createWasPressed = createIsPressed;
            joinWasPressed = joinIsPressed;
            readyWasPressed = readyIsPressed;
        }

        private void OnGUI()
        {
            if (!(GetComponent<GameInstance>()?.RuntimeWorld?.GameMode is INetworkLobby lobby))
            {
                GUI.Label(new Rect(16, 16, 460, 24), status);
                return;
            }

            status = lobby.NetworkStatus;
            GUI.Box(new Rect(16, 16, 320, 142), "Network Lobby");
            GUI.Label(new Rect(28, 44, 296, 22), status);
            GUI.Label(new Rect(28, 66, 296, 22), string.IsNullOrEmpty(lobby.RoomId) ? "Room: <none>" : $"Room: {lobby.RoomId}");
            if (GUI.Button(new Rect(28, 92, 90, 28), "Create")) Run(lobby.CreateRoomAsync);
            roomIdToJoin = GUI.TextField(new Rect(124, 92, 104, 28), roomIdToJoin);
            if (GUI.Button(new Rect(234, 92, 86, 28), "Join")) Run(() => lobby.JoinRoomAsync(roomIdToJoin));
            if (GUI.Button(new Rect(28, 124, 90, 24), "Ready")) Run(() => lobby.SetReadyAsync(true));
            GUI.Label(new Rect(124, 124, 196, 24), "Keys: C Create, J clipboard Join, R Ready");
        }

        private async void Run(Func<System.Threading.Tasks.Task> operation)
        {
            try
            {
                await operation();
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogError($"[Network][034] Lobby request failed: {exception.Message}");
            }
        }
    }
}
