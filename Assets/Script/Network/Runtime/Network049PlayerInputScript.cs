using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame.Network
{
    /// <summary>
    /// Acceptance-only Player input source. It is opt-in through the command-line
    /// switch and feeds the same Input System controls as a real keyboard/mouse.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Network049PlayerInputScript : MonoBehaviour
    {
        private int state;
        private int framesRemaining;
        private bool createQueued;
        private bool joinQueued;
        private bool readyQueued;
        private bool isJoiner;
        private int fireCount = 1;
        private string roomIdToJoin;
        private string capturePath;
        private string beforeCapturePath;
        private bool captureScheduled;
        private bool beforeCaptureTaken;
        private bool acceptanceFinished;
        private bool lobbyOnly;
        private float readyDelaySeconds;
        private float roomEnteredAt = -1f;

        private void Awake()
        {
            roomIdToJoin = FindArgumentValue("-network049-room");
            isJoiner = !string.IsNullOrWhiteSpace(roomIdToJoin);
            fireCount = isJoiner ? 2 : 1;
            capturePath = FindArgumentValue("-network049-capture-path");
            beforeCapturePath = FindArgumentValue("-network049-before-capture-path");
            lobbyOnly = HasArgument("-network049-lobby-only");
            float.TryParse(FindArgumentValue("-network049-ready-delay"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                out readyDelaySeconds);
            readyDelaySeconds = Mathf.Max(0f, readyDelaySeconds);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Debug.Log($"[Network][070] AcceptanceWindowFocus Focused={hasFocus}");
        }

        private void Update()
        {
            if (!Application.isFocused) return;
            GameInstance instance = GetComponent<GameInstance>();
            if (instance?.RuntimeWorld == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            if (!instance.RuntimeWorld.IsGameplayReady)
            {
                QueueLobbyInput(instance.RuntimeWorld.GameMode as INetworkLobby, keyboard);
                return;
            }

            if (lobbyOnly)
            {
                enabled = false;
                return;
            }

            if (acceptanceFinished)
            {
                return;
            }

            if (framesRemaining > 0)
            {
                framesRemaining--;
                return;
            }

            switch (state++)
            {
                case 0:
                    framesRemaining = 120;
                    Debug.Log("[Network][049] PlayerAcceptanceInput WaitInitialWeapon");
                    break;
                case 1:
                    InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Digit2));
                    framesRemaining = 180;
                    Debug.Log("[Network][049] PlayerAcceptanceInput SelectAk12");
                    break;
                case 2:
                    InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(isJoiner ? 178f : 57f, 0f));
                    // Server-confirmed Unequip/Equip can span more than one local
                    // animation window. Do not inject Fire until the selected
                    // weapon has had a stable authority/arming interval.
                    framesRemaining = 120;
                    Debug.Log("[Network][049] PlayerAcceptanceInput AimEnemyPoint3");
                    break;
                case 3:
                case 5:
                    if (state == 4 && !beforeCaptureTaken && !string.IsNullOrWhiteSpace(beforeCapturePath))
                    {
                        beforeCaptureTaken = true;
                        ScreenCapture.CaptureScreenshot(beforeCapturePath);
                        framesRemaining = 3;
                        state--;
                        Debug.Log($"[Network][049] PlayerAcceptanceInput CaptureBeforeFire Path={beforeCapturePath}");
                        break;
                    }
                    if (state == 6 && fireCount < 2)
                    {
                        FinishAcceptanceCapture();
                        break;
                    }
                    InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)1);
                    Debug.Log($"[Network][049] PlayerAcceptanceInput FirePress Index={(state == 4 ? 1 : 2)}");
                    break;
                case 4:
                case 6:
                    InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)0);
                    framesRemaining = state == 5 && fireCount > 1 ? 45 : 0;
                    Debug.Log($"[Network][049] PlayerAcceptanceInput FireRelease Index={(state == 5 ? 1 : 2)}");
                    break;
                default:
                    FinishAcceptanceCapture();
                    break;
            }
        }

        private void FinishAcceptanceCapture()
        {
            Debug.Log($"[Network][049] PlayerAcceptanceInput Finish CapturePath={capturePath}");
            if (!captureScheduled && !string.IsNullOrWhiteSpace(capturePath))
            {
                captureScheduled = true;
                StartCoroutine(CaptureFinalFrame());
            }

            acceptanceFinished = true;
        }

        private IEnumerator CaptureFinalFrame()
        {
            for (int i = 0; i < 90; i++)
            {
                yield return null;
            }

            ScreenCapture.CaptureScreenshot(capturePath);
            Debug.Log($"[Network][049] PlayerAcceptanceInput CaptureFinalFrame Path={capturePath}");
        }

        private void QueueLobbyInput(INetworkLobby lobby, Keyboard keyboard)
        {
            if (lobby == null)
            {
                return;
            }

            if (roomEnteredAt < 0f && !string.IsNullOrEmpty(lobby.RoomId))
                roomEnteredAt = Time.realtimeSinceStartup;
            bool canReady = roomEnteredAt >= 0f && Time.realtimeSinceStartup - roomEnteredAt >= readyDelaySeconds;

            if (isJoiner)
            {
                if (!joinQueued && string.IsNullOrEmpty(lobby.RoomId))
                {
                    GUIUtility.systemCopyBuffer = roomIdToJoin;
                    InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.J));
                    joinQueued = true;
                    Debug.Log($"[Network][049] PlayerAcceptanceInput JoinRoom RoomId={roomIdToJoin}");
                    return;
                }

                if (joinQueued && !readyQueued && canReady)
                {
                    InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.R));
                    readyQueued = true;
                    Debug.Log("[Network][049] PlayerAcceptanceInput ReadyRoom");
                    return;
                }

                if (joinQueued || readyQueued)
                {
                    InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                }

                return;
            }

            if (!createQueued && string.IsNullOrEmpty(lobby.RoomId))
            {
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.C));
                createQueued = true;
                Debug.Log("[Network][049] PlayerAcceptanceInput CreateRoom");
                return;
            }

            if (createQueued && !readyQueued && canReady)
            {
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.R));
                readyQueued = true;
                Debug.Log("[Network][049] PlayerAcceptanceInput ReadyRoom");
                return;
            }

            if (createQueued || readyQueued)
            {
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            }
        }

        public static bool IsEnabledByCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], "-network049-input-script", StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static bool HasArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static string FindArgumentValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            }

            return string.Empty;
        }
    }
}
