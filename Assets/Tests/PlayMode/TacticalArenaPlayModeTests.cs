using System;
using System.Collections;
using System.IO;
using CGame.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class TacticalArenaPlayModeTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private GameInstance instance;
        private UnityEditor.EditorWindow window;
        private Coroutine capture;
        private Coroutine motionObserver;
        private readonly System.Collections.Generic.HashSet<EnemyPresentation> movingEnemies = new System.Collections.Generic.HashSet<EnemyPresentation>();
        private ClientNetworkSubSystem observedNetwork;
        private Action<FireCommitted> fireObserver;

        [UnityTest]
        public IEnumerator SinglePlayerReady_LoadsArenaAndAuthoritativeEnemies()
        {
            yield return StartArena();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.harness/runs/TacticalArena-071-20260911"));
            Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "arena-mainline.png"));
            yield return new WaitForSeconds(.3f);
        }

        [UnityTest]
        public IEnumerator Combat_ReplicatesCoverAndStationaryFire() => ObserveCombat(false);

        [UnityTest]
        public IEnumerator PlayerCombat_RecordsArenaMovementCoverAndWeaponHit() => ObserveCombat(true);

        [UnityTest]
        public IEnumerator PlayerCombat_ApproachesFromWest() => ObserveCombat(true, 1);

        [UnityTest]
        public IEnumerator PlayerCombat_ApproachesFromSouth() => ObserveCombat(true, 2);

        private IEnumerator ObserveCombat(bool finalAcceptance, int approach = 0)
        {
            yield return StartArena();
            mouse = InputSystem.AddDevice<Mouse>();
            var controller = (PlayerController)instance.RuntimeWorld.GameMode.PlayerController;
            var pawn = controller.PossessedPawn;
            if (finalAcceptance)
            {
                var equipment = pawn.GetComponent<EquipmentManagerComponent>();
                yield return WaitFor(() => equipment.CurrentWeapon != null && equipment.CurrentWeapon.IsArmed &&
                    !equipment.IsSwitchInProgress, 10f, "Initial equipment ready before switch input");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
                yield return new WaitForSeconds(.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitFor(() => equipment.CurrentWeapon.ItemHandle == controller.QuickBar.Slots[1] &&
                    equipment.CurrentWeapon.IsArmed && !equipment.IsSwitchInProgress, 10f, "Formal rifle selection");
            }
            var enemies = UnityEngine.Object.FindObjectsOfType<EnemyPresentation>();
            var previous = new System.Collections.Generic.Dictionary<EnemyPresentation, EnemyBrainState>();
            var fired = new System.Collections.Generic.HashSet<EnemyPresentation>();
            var covered = new System.Collections.Generic.HashSet<EnemyPresentation>();
            var moved = movingEnemies;
            var concealment = new System.Collections.Generic.Dictionary<EnemyPresentation, Vector3>();
            var observedPeek = new System.Collections.Generic.HashSet<EnemyPresentation>();
            bool sawStationaryFire = false;
            bool sawReturn = false;
            float start = Time.realtimeSinceStartup;
            float nextSample = 0f;
            bool sawPlayerHit = false;
            float nextShot = 2f;
            float releaseShot = 0f;
            string run = "EnemyCombatReaction-076-20260912";
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.harness/runs/" + run,
                "capture-" + DateTime.Now.ToString("HHmmss")));
            Directory.CreateDirectory(directory);
            capture = instance.StartCoroutine(CaptureFrames(directory, 35f));
            var trace = new System.Text.StringBuilder();
            bool confirmedOwnerHit = false;
            observedNetwork = instance.RuntimeWorld.GetSubSystem<ClientNetworkSubSystem>();
            long ownerPawnId = observedNetwork.ClientWorld.ControlledPawnId;
            fireObserver = shot =>
            {
                if (shot.PawnId != ownerPawnId) return;
                confirmedOwnerHit |= shot.HitEnemyId > 0;
                trace.AppendLine($"  ownerFireConfirmed pawn={shot.PawnId} sequence={shot.ShotSequence} tick={shot.ServerTick} hitEnemy={shot.HitEnemyId} ammo={shot.AuthoritativeMagazineAmmo}");
            };
            observedNetwork.FireCommittedReceived += fireObserver;
            Vector3 approachPoint = approach == 0 ? new Vector3(8, 0, 6) :
                approach == 1 ? new Vector3(-8, 0, 6) : new Vector3(0, 0, -5);
            bool reachedApproach = false;
            float movementStartedAt = -1f;
            trace.AppendLine($"approach={approach} waypoint={approachPoint:F2}");
            while (Time.realtimeSinceStartup - start < 35f && instance.RuntimeWorld.IsGameplayReady)
            {
                if (!Application.isFocused)
                {
                    window.Focus();
                    yield return null;
                    continue;
                }
                float elapsed = Time.realtimeSinceStartup - start;
                reachedApproach |= Vector3.ProjectOnPlane(pawn.Transform.position - approachPoint, Vector3.up).sqrMagnitude < 6.25f;
                foreach (var enemy in enemies)
                {
                    if (enemy == null) continue;
                    var state = enemy.RemoteAnimationState;
                    if (state.IsMoving) moved.Add(enemy);
                    if (state.BrainState == EnemyBrainState.CoverHold && !enemy.IsDead)
                    {
                        covered.Add(enemy);
                        // Same-position low-cover ReturnToCover lasts one tick
                        // and can fall between snapshots. Verify its resulting
                        // concealed position and rest, not just that transient enum.
                        if (observedPeek.Contains(enemy) && concealment.TryGetValue(enemy, out var hidden))
                            sawReturn |= Vector3.Distance(hidden, enemy.transform.position) < .2f && state.Speed <= .05f;
                        concealment[enemy] = enemy.transform.position;
                    }
                    if (state.IsPeeking) observedPeek.Add(enemy);
                    if (state.BrainState == EnemyBrainState.ReturnToCover) sawReturn = true;
                    if (enemy.LastConfirmedAction == EnemyActionKind.Fire) fired.Add(enemy);
                    sawPlayerHit |= enemy.LastConfirmedAction == EnemyActionKind.Hit || enemy.IsDead;
                    sawStationaryFire |= state.BrainState == EnemyBrainState.Fire && state.Speed <= .05f &&
                        enemy.LastConfirmedAction == EnemyActionKind.Fire;
                    Assert.That(state.IsMoving && state.IsPeeking, Is.False,
                        "A moving enemy must not select the stationary peek pose.");
                    if (elapsed >= nextSample || !previous.TryGetValue(enemy, out var last) || last != state.BrainState)
                    {
                        Vector3 aim = Vector3.ProjectOnPlane(pawn.Transform.position - enemy.transform.position, Vector3.up);
                        Vector3 forearm = enemy.Animator.GetBoneTransform(HumanBodyBones.RightHand).position -
                            enemy.Animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
                        trace.AppendLine($"{elapsed:F2} {enemy.name} {state.BrainState} pos={enemy.transform.position:F2} speed={state.Speed:F2} direction={state.MoveDirection:F2} action={enemy.LastConfirmedAction} owner={pawn.Transform.position:F2} rootAim={Vector3.Angle(enemy.transform.forward, aim):F1} armRoot={Vector3.Angle(Vector3.ProjectOnPlane(forearm, Vector3.up), enemy.transform.forward):F1}");
                        Vector3 leftFoot = enemy.Animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                        Vector3 rightFoot = enemy.Animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
                        trace.AppendLine($"  grounded={state.IsGrounded} leftFoot={leftFoot:F3} rightFoot={rightFoot:F3} moveX={enemy.Animator.GetFloat("MoveX"):F3} moveY={enemy.Animator.GetFloat("MoveY"):F3} peeking={state.IsPeeking} aiming={state.IsAiming}");
                        trace.AppendLine($"  focused={Application.isFocused} keyboard={keyboard.deviceId}/{Keyboard.current?.deviceId} enabled={keyboard.enabled} mouse={mouse.deviceId}/{Mouse.current?.deviceId}");
                        previous[enemy] = state.BrainState;
                    }
                }
                if (elapsed >= nextSample)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"combat-{Mathf.FloorToInt(elapsed):D2}.png"));
                    nextSample = elapsed + 2f;
                }
                if (finalAcceptance && sawReturn)
                {
                    // Observe a complete stable-target cover cycle before changing
                    // its sight lines/range with the approach route under test.
                    if (movementStartedAt < 0f) movementStartedAt = elapsed;
                    float movementElapsed = elapsed - movementStartedAt;
                    Vector3 waypoint = movementElapsed < 4 ? new Vector3(5, 0, 1) :
                        movementElapsed < 20 ? new Vector3(8, 0, 6) : movementElapsed < 27 ? new Vector3(6, 0, 5) : new Vector3(2, 0, 3);
                    if (approach == 1) waypoint.x = -waypoint.x;
                    if (approach == 2)
                        waypoint = movementElapsed < 4 ? new Vector3(0, 0, -1) : movementElapsed < 22 ? approachPoint : new Vector3(2, 0, 1);
                    Vector3 local = Quaternion.Inverse(Quaternion.Euler(0, controller.ControlYaw, 0)) *
                        Vector3.ProjectOnPlane(waypoint - pawn.Transform.position, Vector3.up);
                    var keys = new System.Collections.Generic.List<Key>();
                    if (local.magnitude > .5f)
                    {
                        if (Mathf.Abs(local.x) > .4f) keys.Add(local.x > 0 ? Key.D : Key.A);
                        if (Mathf.Abs(local.z) > .4f) keys.Add(local.z > 0 ? Key.W : Key.S);
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                }
                else InputSystem.QueueStateEvent(keyboard, finalAcceptance || elapsed < 8 ? new KeyboardState() :
                    elapsed < 11 ? new KeyboardState(Key.A) : elapsed < 14 ? new KeyboardState(Key.D) : new KeyboardState());
                EnemyPresentation focus = null;
                float nearest = float.MaxValue;
                foreach (var enemy in enemies)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    float distance = Vector3.Distance(enemy.transform.position, pawn.Transform.position);
                    if (!finalAcceptance && elapsed < 8 && enemy.name.Contains("Rifle"))
                        distance -= 100f;
                    // Combat input must address the nearest threat immediately;
                    // a fixed cinematic target schedule previously let the owner
                    // die before the first scheduled shot at sixteen seconds.
                    if (distance < nearest) { nearest = distance; focus = enemy; }
                }
                if (focus != null)
                {
                    Vector3 aim = focus.transform.position + Vector3.up * 1.2f - pawn.GetComponent<PawnCameraComponent>().Camera.transform.position;
                    float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                    InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(
                        Mathf.Clamp(Mathf.DeltaAngle(controller.ControlYaw, yaw), -2, 2),
                        Mathf.Clamp(controller.ControlPitch - pitch, -2, 2)));
                    // First observe a complete cover cycle before killing its
                    // participant. Other exposed threats can still be engaged.
                    if (finalAcceptance && (sawReturn || (!focus.RemoteAnimationState.IsInCover &&
                        focus.RemoteAnimationState.BrainState != EnemyBrainState.TakeCover)) && elapsed >= nextShot &&
                        Mathf.Abs(Mathf.DeltaAngle(controller.ControlYaw, yaw)) < 2f && Mathf.Abs(controller.ControlPitch - pitch) < 2f)
                    {
                        InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)1);
                        trace.AppendLine($"  playerFireInput={elapsed:F2} target={focus.name} yawError={Mathf.DeltaAngle(controller.ControlYaw, yaw):F2} pitchError={controller.ControlPitch - pitch:F2}");
                        nextShot = elapsed + .6f;
                        releaseShot = elapsed + .1f;
                    }
                }
                if (releaseShot > 0f && elapsed >= releaseShot)
                {
                    InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)0);
                    releaseShot = 0f;
                }
                yield return null;
            }
            File.WriteAllText(Path.Combine(directory, "combat-trace.txt"), trace.ToString());
            Debug.Log($"[Arena076] approach={approach} reached={reachedApproach} moved={moved.Count} fired={fired.Count} covered={covered.Count} stationaryFire={sawStationaryFire} return={sawReturn} playerHit={sawPlayerHit} evidence={directory}");
            Assert.That(moved.Count, Is.EqualTo(3), "All three authority enemies move.");
            Assert.That(fired.Count, Is.GreaterThanOrEqualTo(1), "Authority confirms fire.");
            Assert.That(covered.Count, Is.GreaterThanOrEqualTo(1), "An enemy actually reaches concealment.");
            Assert.That(sawReturn, Is.True, "A peek is followed by physical return.");
            Assert.That(sawStationaryFire, Is.True, "A firing enemy settles into stationary aim.");
            if (finalAcceptance)
            {
                Assert.That(sawPlayerHit && confirmedOwnerHit, Is.True,
                    "Real mouse Fire must confirm this owner's HitEnemyId and an enemy Hit presentation.");
                Assert.That(reachedApproach, Is.True, "Formal movement must reach the specified approach, not only press keys.");
            }
        }

        private static IEnumerator CaptureFrames(string directory, float duration)
        {
            string frames = Path.Combine(directory, "frames");
            Directory.CreateDirectory(frames);
            string timestamps = Path.Combine(directory, "timestamps.csv");
            File.WriteAllText(timestamps, "frame,seconds\n");
            double start = Time.realtimeSinceStartupAsDouble;
            double nextFrame = start;
            int frame = 0;
            while (Time.realtimeSinceStartupAsDouble - start < duration)
            {
                yield return new WaitForEndOfFrame();
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < nextFrame) continue;
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(frames, frame.ToString("D5") + ".jpg"), texture.EncodeToJPG(85)); }
                finally { UnityEngine.Object.Destroy(texture); }
                File.AppendAllText(timestamps, frame + "," + (now - start).ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "\n");
                frame++;
                nextFrame = start + Math.Floor((now - start) * 30d + 1d) / 30d;
            }
        }

        private IEnumerator StartArena()
        {
            movingEnemies.Clear();
            if (World.Current != null)
            {
                var shutdown = World.Current.ShutdownAsync();
                yield return WaitFor(() => shutdown.IsCompleted, 15, "Previous World shutdown");
            }
            keyboard = InputSystem.AddDevice<Keyboard>();
            var load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitFor(() => load.isDone, 20, "Load SampleScene");
            instance = UnityEngine.Object.FindObjectOfType<GameInstance>();
            motionObserver = instance.StartCoroutine(ObserveEnemyMotionFromSpawn());
            window = ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as UnityEditor.EditorWindow;
            window.titleContent = new GUIContent("Tactical Arena Verification");
            window.position = new Rect(80, 80, 1280, 760);
            window.ShowUtility();
            window.Focus();
            yield return WaitFor(() => instance.RuntimeWorld?.GameMode is INetworkLobby, 20, "Lobby ready");
            var lobby = (INetworkLobby)instance.RuntimeWorld.GameMode;
            yield return WaitFor(() => lobby.NetworkStatus == "Connected" && Application.isFocused, 20, "Host handshake and focus");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitFor(() => !string.IsNullOrEmpty(lobby.RoomId), 15, "C creates room");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitFor(() => instance.RuntimeWorld.IsGameplayReady, 45, "Single ready starts authority");
            yield return WaitFor(() => UnityEngine.Object.FindObjectsOfType<EnemyPresentation>().Length == 3, 15, "Three authority enemies");
            Assert.That(GameObject.Find("TacticalArenaEnvironment/tree"), Is.Not.Null);
            // Ready/scene activation can return focus to another editor panel.
            // Restore the real GameView; do not bypass the production input gate.
            window.Focus();
            yield return WaitFor(() => Application.isFocused && keyboard.enabled, 10f, "Gameplay input focus after Ready");
            var pawn = ((PlayerController)instance.RuntimeWorld.GameMode.PlayerController).PossessedPawn;
            Vector3 before = pawn.Transform.position;
            Debug.Log($"[Arena076Input] Before move focused={Application.isFocused} keyboard={keyboard.deviceId}/{Keyboard.current?.deviceId} enabled={keyboard.enabled} pos={before:F3}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.25f);
            Debug.Log($"[Arena076Input] After move focused={Application.isFocused} keyboard={keyboard.deviceId}/{Keyboard.current?.deviceId} enabled={keyboard.enabled} pos={pawn.Transform.position:F3}");
            Assert.That(Vector3.Distance(before, pawn.Transform.position), Is.GreaterThan(.3f));
            Debug.Log($"[Arena071] GameplayReady=True Enemies=3 Before={before} After={pawn.Transform.position}");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (observedNetwork != null && fireObserver != null)
                observedNetwork.FireCommittedReceived -= fireObserver;
            observedNetwork = null;
            fireObserver = null;
            if (keyboard?.added == true) InputSystem.RemoveDevice(keyboard);
            if (mouse?.added == true) InputSystem.RemoveDevice(mouse);
            if (capture != null && instance != null) instance.StopCoroutine(capture);
            if (motionObserver != null && instance != null) instance.StopCoroutine(motionObserver);
            motionObserver = null;
            if (instance != null) instance.ShutdownRuntimeWorld();
            if (window != null) window.Close();
            yield return null;
        }

        private IEnumerator ObserveEnemyMotionFromSpawn()
        {
            var firstPositions = new System.Collections.Generic.Dictionary<EnemyPresentation, Vector3>();
            while (instance != null)
            {
                foreach (var enemy in UnityEngine.Object.FindObjectsOfType<EnemyPresentation>())
                {
                    if (!firstPositions.TryGetValue(enemy, out var first))
                    {
                        firstPositions.Add(enemy, enemy.transform.position);
                        continue;
                    }
                    if (enemy.RemoteAnimationState.IsMoving && Vector3.Distance(first, enemy.transform.position) > .1f &&
                        movingEnemies.Add(enemy))
                        Debug.Log($"[Arena076SpawnMotion] enemy={enemy.name} from={first:F3} to={enemy.transform.position:F3} state={enemy.RemoteAnimationState.BrainState}");
                }
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
