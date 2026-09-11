using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CGame.Network
{
    /// <summary>Opt-in device events for the two-player mainline acceptance route.</summary>
    public sealed class NetworkPvePlayerInputScript : MonoBehaviour
    {
        private float elapsed;
        private int phase = -1;
        private bool isSupport;
        private string capturePath;
        private EnemyPresentation target;
        private float nextShot;
        private float releaseShotAt;
        private int shots;
        private bool sawDeath;
        private float deathSeenAt;
        private bool approaching;
        private bool capturedFall;
        private bool reloadQueued;
        private float releaseReloadAt;

        public static bool IsEnabledByCommandLine() =>
            Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "-network-pve-input-route");

        private void Awake()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            isSupport = Array.Exists(arguments, argument => argument == "-network049-room");
            for (int index = 0; index + 1 < arguments.Length; index++)
                if (arguments[index] == "-network-pve-capture-path") capturePath = arguments[index + 1];
        }

        private void Update()
        {
            if (!Application.isFocused || GetComponent<GameInstance>()?.RuntimeWorld?.IsGameplayReady != true ||
                Keyboard.current == null) return;
            elapsed += Time.unscaledDeltaTime;
            float advanceAt = isSupport ? 20f : 24f;
            float advanceDuration = isSupport ? 4f : 2.5f;
            int nextPhase = elapsed < 2f ? 0 : elapsed < 2.2f ? 1 :
                !isSupport && elapsed >= 4f && elapsed < 5.2f ? 5 :
                elapsed < advanceAt ? 2 : elapsed < advanceAt + advanceDuration ? 3 :
                !isSupport && elapsed >= 30f && elapsed < 33.2f ? 6 : 4;
            if (nextPhase != phase)
            {
                phase = nextPhase;
                Key[] keys = phase == 1 ? new[] { Key.Digit2 } : phase == 3 ? new[] { Key.W } :
                    phase == 5 ? new[] { Key.A } : phase == 6 ? new[] { Key.D } : Array.Empty<Key>();
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
                Debug.Log($"[Network][070] PveInput Role={(isSupport ? "Support" : "Lead")} Phase={phase} Focused={Application.isFocused}");
                if (phase == 3 || phase == 4) capture("phase" + phase);
            }
            Camera camera = Camera.allCameras.OrderByDescending(candidate => candidate.depth).FirstOrDefault();
            if (camera != null && Mouse.current != null && elapsed >= 6f && elapsed < (isSupport ? 36f : 40f))
            {
                Vector3 direction = Vector3.forward * 10f;
                if (!isSupport && elapsed < 20f)
                {
                    EnemyPresentation[] patrols = FindObjectsOfType<EnemyPresentation>().OrderBy(enemy => enemy.name).ToArray();
                    int index = Mathf.Min((int)((elapsed - 6f) / 4.5f), patrols.Length - 1);
                    if (index >= 0) direction = patrols[index].transform.position + Vector3.up * 1.3f - camera.transform.position;
                }
                else if (!isSupport && elapsed >= 33.5f)
                {
                    EnemyPresentation ak = FindObjectsOfType<EnemyPresentation>()
                        .FirstOrDefault(enemy => !enemy.IsDead && enemy.name.Contains("Ak"));
                    if (ak != null) direction = ak.transform.position + Vector3.up * 1.3f - camera.transform.position;
                }
                aim(camera, direction, 1.5f);
            }
            if (phase != 4) return;
            driveCombat();
            if (elapsed < 64f) return;
            capture("complete");
            enabled = false;
        }

        private void driveCombat()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (releaseShotAt > 0f && elapsed >= releaseShotAt)
            {
                InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)0);
                releaseShotAt = 0f;
            }
            if (releaseReloadAt > 0f && elapsed >= releaseReloadAt)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                releaseReloadAt = 0f;
            }
            if (elapsed < (isSupport ? 37f : 40f)) return;
            Camera camera = Camera.allCameras.OrderByDescending(candidate => candidate.depth).FirstOrDefault();
            if (camera == null) return;
            if (sawDeath && elapsed >= deathSeenAt + 3f)
            {
                string observedVariant = elapsed < deathSeenAt + 8f ? "Rifle" : "Ak";
                if (target == null || !target.name.Contains(observedVariant))
                {
                    target = FindObjectsOfType<EnemyPresentation>()
                        .FirstOrDefault(enemy => !enemy.IsDead && enemy.name.Contains(observedVariant));
                    if (target != null) Debug.Log($"[Network][070] PveObserve Name={target.name} Elapsed={elapsed:F2}");
                }
            }
            if (target == null && (!sawDeath || elapsed >= deathSeenAt + 3f))
            {
                target = FindObjectsOfType<EnemyPresentation>().Where(enemy => !enemy.IsDead && hasClearAim(camera, enemy))
                    .OrderBy(enemy => Vector3.Distance(camera.transform.position, enemy.transform.position)).FirstOrDefault();
                if (target != null)
                    Debug.Log($"[Network][070] PveTarget Role={(isSupport ? "Support" : "Lead")} Name={target.name} Camera={camera.transform.position} Target={target.transform.position} Distance={Vector3.Distance(camera.transform.position, target.transform.position):F3}");
            }
            if (target != null)
            {
                if (target.IsDead && !sawDeath)
                {
                    sawDeath = true;
                    deathSeenAt = elapsed;
                    capture("death");
                }
                Vector3 direction = target.transform.position + Vector3.up * (target.IsDead ? .25f : 1.3f) - camera.transform.position;
                Vector2 aimError = aim(camera, direction, 1.5f);
                bool advance = !isSupport && !sawDeath && !target.IsDead && direction.magnitude > 6f &&
                    elapsed < 50f && Mathf.Abs(aimError.x) < 8f && hasClearAim(camera, target);
                if (advance != approaching)
                {
                    approaching = advance;
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(advance ? new[] { Key.W } : Array.Empty<Key>()));
                    Debug.Log($"[Network][070] PveApproach Moving={advance} Distance={direction.magnitude:F3}");
                }
                if (sawDeath && !capturedFall && elapsed >= deathSeenAt + 1f)
                {
                    capturedFall = true;
                    capture("fallen");
                }
                if (!advance && (isSupport || direction.magnitude <= 6.5f) && !sawDeath && !target.IsDead && (!isSupport || shots < 3) && elapsed >= nextShot &&
                    Mathf.Abs(aimError.x) < 1f && Mathf.Abs(aimError.y) < 1f && hasClearAim(camera, target))
                {
                    InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)1);
                    shots++;
                    releaseShotAt = elapsed + .08f;
                    nextShot = elapsed + .8f;
                    Debug.Log($"[Network][070] PveFireInput Role={(isSupport ? "Support" : "Lead")} Shot={shots}");
                }
            }
            if (!reloadQueued && elapsed >= nextShot && (sawDeath && elapsed >= deathSeenAt + 8f || isSupport && shots >= 3))
            {
                reloadQueued = true;
                releaseReloadAt = elapsed + .15f;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.R));
                Debug.Log("[Network][070] PveReloadInput");
            }
        }

        private static Vector2 aim(Camera camera, Vector3 direction, float maximumDelta)
        {
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            var error = new Vector2(Mathf.DeltaAngle(camera.transform.eulerAngles.y, yaw),
                Mathf.DeltaAngle(pitch, camera.transform.eulerAngles.x));
            InputSystem.QueueDeltaStateEvent(Mouse.current.delta, new Vector2(
                Mathf.Clamp(error.x, -maximumDelta, maximumDelta), Mathf.Clamp(error.y, -maximumDelta, maximumDelta)));
            return error;
        }

        private static bool hasClearAim(Camera camera, EnemyPresentation enemy)
        {
            Vector3 direction = enemy.transform.position + Vector3.up * 1.3f - camera.transform.position;
            return !Physics.RaycastAll(camera.transform.position, direction.normalized, direction.magnitude - .2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(hit =>
                !hit.transform.IsChildOf(camera.transform.root) && !hit.transform.IsChildOf(enemy.transform));
        }

        private void capture(string suffix)
        {
            if (suffix == "fallen" && target != null)
            {
                float lowestVertex = float.PositiveInfinity;
                foreach (var renderer in target.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh();
                    renderer.BakeMesh(mesh);
                    foreach (Vector3 vertex in mesh.vertices)
                        lowestVertex = Mathf.Min(lowestVertex, renderer.transform.TransformPoint(vertex).y);
                    Destroy(mesh);
                }
                Debug.Log($"[Network][070] PveDeathGeometry Name={target.name} RootY={target.transform.position.y:F4} LowestVertexY={lowestVertex:F4}");
            }
            if (string.IsNullOrWhiteSpace(capturePath)) return;
            string path = System.IO.Path.ChangeExtension(capturePath, null) + "-" + suffix + ".png";
            ScreenCapture.CaptureScreenshot(path);
        }

        private void OnDisable()
        {
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            if (Mouse.current != null)
            {
                InputSystem.QueueStateEvent(Mouse.current, new MouseState
                {
                    position = Mouse.current.position.ReadValue()
                });
            }
        }
    }
}
