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
        private EnemyPresentation observedDeathTarget;
        private float observedDeathAt;
        private float nextShot;
        private float releaseShotAt;
        private int shots;
        private bool sawDeath;
        private float deathSeenAt;
        private bool approaching;
        private bool capturedFall;
        private bool reloadQueued;
        private float releaseReloadAt;
        private bool openingComplete;
        private int openingStage;
        private float openingStageAt;
        private int openingSubject = -1;
        private float nextOpeningTrace;
        private bool openingEquipQueued;
        private float openingEquipAt;
        private int observationPoint;
        private readonly Vector3[] observationRoute =
        {
            new Vector3(8f, 0f, 4f), new Vector3(5f, 0f, 1f),
            new Vector3(-5f, 0f, 1f), new Vector3(-8f, 0f, 6f)
        };

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
            if (!openingComplete)
            {
                driveOpeningObservation();
                return;
            }
            if (phase != 4)
            {
                phase = 4;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                Debug.Log($"[Network][070] PveInput Role={(isSupport ? "Support" : "Lead")} Phase={phase} Focused={Application.isFocused}");
                capture("phase" + phase);
            }
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
            if (elapsed < 3f) return;
            Camera camera = Camera.allCameras.OrderByDescending(candidate => candidate.depth).FirstOrDefault();
            if (camera == null) return;
            if (target != null && target.IsDead && observedDeathTarget != target)
            {
                observedDeathTarget = target;
                observedDeathAt = elapsed;
                Debug.Log($"[Network][077] ObserveDeath Name={target.name} Elapsed={elapsed:F2}");
            }
            bool observingDeath = target != null && target.IsDead && elapsed < observedDeathAt + 1.5f;
            if (!observingDeath && sawDeath && elapsed >= deathSeenAt + 8f)
            {
                // A protected observation subject must not monopolize self-defence
                // while a closer, visible enemy continues firing at the owner.
                EnemyPresentation threat = FindObjectsOfType<EnemyPresentation>()
                    .Where(enemy => !enemy.IsDead && hasClearAim(camera, enemy))
                    .OrderBy(enemy => Vector3.Distance(camera.transform.position, enemy.transform.position)).FirstOrDefault();
                if (threat != null && target != threat)
                {
                    target = threat;
                    Debug.Log($"[Network][077] DefendTarget Name={target.name} Elapsed={elapsed:F2}");
                }
            }
            if (!observingDeath && sawDeath && elapsed >= deathSeenAt + 3f && (target == null || target.IsDead))
            {
                target = FindObjectsOfType<EnemyPresentation>().Where(enemy => !enemy.IsDead)
                    .OrderByDescending(enemy => enemy.name.Contains("Rifle"))
                    .ThenBy(enemy => Vector3.Distance(camera.transform.position, enemy.transform.position)).FirstOrDefault();
                if (target != null) Debug.Log($"[Network][070] PveObserve Name={target.name} Elapsed={elapsed:F2}");
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
                bool advance = openingComplete && !isSupport && !sawDeath && !target.IsDead && direction.magnitude > 14f &&
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
                // Observe one complete cover burst before resuming normal self-defence.
                // The recording driver must not stop fighting permanently after its first kill.
                bool canDefend = !sawDeath || elapsed >= deathSeenAt + 8f;
                if (!advance && (!openingComplete || isSupport || sawDeath || direction.magnitude <= 14.5f) && canDefend && !target.IsDead && (!isSupport || shots < 3) && elapsed >= nextShot &&
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
            if (openingComplete && !isSupport && sawDeath && elapsed >= deathSeenAt + 3f && releaseReloadAt <= 0f)
                moveToObservationPoint(camera);
        }

        private void driveOpeningObservation()
        {
            Camera camera = Camera.allCameras.OrderByDescending(candidate => candidate.depth).FirstOrDefault();
            if (camera == null || Mouse.current == null) return;
            if (openingStage == 0)
            {
                EnemyPresentation patrolSubject = FindObjectsOfType<EnemyPresentation>()
                    .FirstOrDefault(enemy => enemy.name.Contains(isSupport ? "Ak" : "Rifle"));
                if (patrolSubject != null)
                {
                    aim(camera, patrolSubject.transform.position + Vector3.up - camera.transform.position, 3f);
                    traceOpeningSubject(camera, patrolSubject);
                }
                if (!moveToPoint(camera, new Vector3(isSupport ? 2f : -2f, 0f, -8f), true)) return;
                openingStage = 1;
                openingStageAt = elapsed;
                Debug.Log($"[Network][077] OpeningVantage Role={(isSupport ? "Support" : "Lead")} Position={camera.transform.position:F3} Elapsed={elapsed:F2}");
                return;
            }
            if (openingStage == 1)
            {
                // Direct slot selection rejects running intent or residual speed.
                // Release movement, let the motor brake, then select and arm normally.
                float resting = elapsed - openingStageAt;
                if (resting < .5f)
                {
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    return;
                }
                if (!openingEquipQueued)
                {
                    openingEquipQueued = true;
                    openingEquipAt = elapsed;
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Digit2));
                    Debug.Log($"[Network][077] OpeningEquipInput Elapsed={elapsed:F2}");
                    return;
                }
                if (elapsed - openingEquipAt < 1.5f)
                {
                    if (elapsed - openingEquipAt >= .2f)
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    return;
                }
                if (isSupport)
                {
                    openingComplete = true;
                    return;
                }
                driveCombat();
                if (!sawDeath || elapsed < deathSeenAt + 3f) return;
                openingStage = 2;
                openingStageAt = elapsed;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            }
            if (openingStage == 2)
            {
                float observing = elapsed - openingStageAt;
                int subject = observing < 6f ? 0 : 1;
                EnemyPresentation enemy = FindObjectsOfType<EnemyPresentation>()
                    .FirstOrDefault(candidate => !candidate.IsDead && candidate.name.Contains(subject == 0 ? "Rifle" : "Ak"));
                if (enemy != null)
                {
                    aim(camera, enemy.transform.position + Vector3.up - camera.transform.position, 3f);
                    traceOpeningSubject(camera, enemy);
                    if (openingSubject != subject)
                    {
                        openingSubject = subject;
                        Debug.Log($"[Network][077] OpeningObserve Name={enemy.name} State={enemy.RemoteAnimationState.BrainState} Elapsed={elapsed:F2}");
                    }
                }
                if (observing < 12f) return;
                openingStage = 3;
            }
            if (!moveToPoint(camera, new Vector3(0f, 0f, 3f), true)) return;
            openingComplete = true;
            Debug.Log($"[Network][077] OpeningComplete Position={camera.transform.position:F3} Elapsed={elapsed:F2}");
        }

        private void moveToObservationPoint(Camera camera)
        {
            var controller = GetComponent<GameInstance>()?.RuntimeWorld?.GameMode?.PlayerController;
            if (controller?.PossessedActor == null) return;
            // The owner camera provides the planar observation origin without
            // coupling this input adapter to the concrete character assembly.
            Vector3 position = camera.transform.position;
            while (observationPoint < observationRoute.Length &&
                Vector3.ProjectOnPlane(observationRoute[observationPoint] - position, Vector3.up).magnitude < .7f)
            {
                Debug.Log($"[Network][077] ObservationPoint Reached={observationPoint} Position={position:F3} Elapsed={elapsed:F2}");
                observationPoint++;
            }
            if (observationPoint < observationRoute.Length) moveToPoint(camera, observationRoute[observationPoint], false);
            else InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        }

        private bool moveToPoint(Camera camera, Vector3 destination, bool sprint)
        {
            var controller = GetComponent<GameInstance>()?.RuntimeWorld?.GameMode?.PlayerController;
            if (controller?.PossessedActor == null) return false;
            Vector3 delta = Vector3.ProjectOnPlane(destination - camera.transform.position, Vector3.up);
            bool arrived = delta.magnitude < .7f;
            var keys = new System.Collections.Generic.List<Key>();
            if (!arrived)
            {
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, controller.ControlYaw, 0f)) * delta;
                if (Mathf.Abs(local.x) > .4f) keys.Add(local.x > 0f ? Key.D : Key.A);
                if (Mathf.Abs(local.z) > .4f) keys.Add(local.z > 0f ? Key.W : Key.S);
                if (sprint) keys.Add(Key.LeftShift);
            }
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys.ToArray()));
            return arrived;
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

        private void traceOpeningSubject(Camera camera, EnemyPresentation enemy)
        {
            if (elapsed < nextOpeningTrace) return;
            nextOpeningTrace = elapsed + .5f;
            Vector3 viewport = camera.WorldToViewportPoint(enemy.transform.position + Vector3.up);
            Debug.Log($"[Network][077] OpeningSubject Name={enemy.name} Elapsed={elapsed:F2} State={enemy.RemoteAnimationState.BrainState} Speed={enemy.RemoteAnimationState.Speed:F2} Position={enemy.transform.position:F2} Viewport={viewport:F2} FeetClear={hasClearAim(camera, enemy, .2f)}");
        }

        private static bool hasClearAim(Camera camera, EnemyPresentation enemy, float height = 1.3f)
        {
            Vector3 direction = enemy.transform.position + Vector3.up * height - camera.transform.position;
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
