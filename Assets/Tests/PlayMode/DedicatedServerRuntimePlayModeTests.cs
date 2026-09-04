using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CGame.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class DedicatedServerRuntimePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return StopSceneWorlds();

            DedicatedServerRuntime[] runtimes = Object.FindObjectsOfType<DedicatedServerRuntime>();
            foreach (DedicatedServerRuntime runtime in runtimes)
            {
                if (runtime != null) Object.Destroy(runtime.gameObject);
            }

            yield return null;
        }

        private static IEnumerator StopSceneWorlds()
        {
            GameInstance[] gameInstances = Object.FindObjectsOfType<GameInstance>();
            foreach (GameInstance gameInstance in gameInstances)
            {
                if (gameInstance != null)
                {
                    gameInstance.ShutdownRuntimeWorld();
                    Object.Destroy(gameInstance);
                }
            }

            yield return null;

            if (World.Current != null)
            {
                Task shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted) yield return null;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartAsync_CreatesAuthorityPawnsAndAdvancesSixtyHertzPhysics()
        {
            yield return StopSceneWorlds();

            var launch = new DedicatedServerLaunchConfiguration(
                17,
                AllocatePort(),
                AllocatePort(),
                "test-credential",
                "SampleScene",
                "v1",
                new[]
                {
                    new DedicatedAuthorityPawnConfiguration(100, 1, 1, "PlayerPoint 1"),
                    new DedicatedAuthorityPawnConfiguration(200, 2, 1, "PlayerPoint 2")
                });
            var root = new GameObject("DedicatedServerRuntimeTest");
            DedicatedServerRuntime runtime = root.AddComponent<DedicatedServerRuntime>();
            Task start = runtime.StartAsync(launch, loadLevel: false);
            while (!start.IsCompleted) yield return null;
            Assert.That(start.IsFaulted, Is.False, start.Exception?.GetBaseException().Message);

            for (int frame = 0; frame < 30 && !runtime.IsPhysicsReady; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(runtime.IsPhysicsReady, Is.True);
            Assert.That(runtime.FixedStepCount, Is.GreaterThan(0));
            Assert.That(runtime.AuthorityPawns, Has.Count.EqualTo(2));
            foreach (Pawn pawn in runtime.AuthorityPawns)
            {
                Assert.That(pawn.TryGetComponent(out NetworkPawnBinding binding), Is.True);
                Assert.That(binding.Role, Is.EqualTo(NetworkPawnRole.Authority));
            }

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        [Category("Network038")]
        [Category("Network039")]
        public IEnumerator DataPlane_AuthenticatedMove_ReturnsOwnerAck()
        {
            yield return StopSceneWorlds();

            int dataPort = AllocatePort();
            var launch = new DedicatedServerLaunchConfiguration(
                17, dataPort, AllocatePort(), "test-credential", "SampleScene", "v1",
                new[] { new DedicatedAuthorityPawnConfiguration(100, 1, 1, "PlayerPoint 1", "connection-a") });
            var root = new GameObject("DedicatedDataPlaneTest");
            DedicatedServerRuntime runtime = root.AddComponent<DedicatedServerRuntime>();
            Task start = runtime.StartAsync(launch, loadLevel: false);
            while (!start.IsCompleted) yield return null;
            Assert.That(start.IsFaulted, Is.False, start.Exception?.GetBaseException().Message);

            var channel = new ClientMovementNetworkChannel(new LiteNetClientNetworkTransport());
            var received = new List<OwnerReconcile>();
            var snapshots = new List<AuthoritySnapshot>();
            channel.OwnerReconcileReceived += response =>
                received.Add(NetworkMessageSerializer.Deserialize<OwnerReconcileWireMessage>(response.Payload).ToValue());
            channel.AuthoritySnapshotReceived += response =>
                snapshots.Add(NetworkMessageSerializer.Deserialize<AuthoritySnapshotWireMessage>(response.Payload).ToValue());
            channel.Connect(17, $"127.0.0.1:{dataPort}", "test-credential:100");
            for (int frame = 0; frame < 60 && !channel.IsConnected; frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(channel.IsConnected, Is.True, "The authenticated data client did not connect.");
            for (int frame = 0; frame < 60 && snapshots.Count < 1; frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(snapshots, Is.Not.Empty, "The Dedicated data server did not broadcast an AuthoritySnapshot.");
            Assert.That(snapshots[0].MatchId, Is.EqualTo(17));
            Assert.That(snapshots[0].PawnId, Is.EqualTo(100));

            long tick = snapshots[snapshots.Count - 1].State.ServerTick;
            channel.Send(new PawnMove(
                17, 100, 1, 3, tick,
                new QuantizedInput(short.MaxValue, 0), new QuantizedView(0, 0), PawnMoveFlags.None,
                new QuantizedVector3(83, 0, 0)));
            for (int frame = 0; frame < 60 && received.Count < 1; frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }

            Assert.That(received, Has.Count.EqualTo(1), "The Dedicated data server did not return an ACK.");
            Assert.That(received[0].Kind, Is.EqualTo(OwnerReconcileKind.Ack));
            Assert.That(received[0].AckSequence, Is.EqualTo(3));

            channel.Send(new PawnMove(
                17, 100, 1, 4, snapshots[snapshots.Count - 1].State.ServerTick,
                new QuantizedInput(short.MaxValue, 0), new QuantizedView(0, 0), PawnMoveFlags.None,
                new QuantizedVector3(5000, 0, 0)));
            for (int frame = 0; frame < 60 && received.Count < 2; frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(received, Has.Count.EqualTo(2), "The Dedicated data server did not return a correction.");
            Assert.That(received[1].Kind, Is.EqualTo(OwnerReconcileKind.Correction));
            Assert.That(received[1].Reason, Is.EqualTo("PositionError"));
            channel.Dispose();
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        [Category("Network038")]
        public IEnumerator FormalBootstrap_CreatesMotorAuthorityWithoutLocalInputOrCamera()
        {
            yield return StopSceneWorlds();

            AsyncOperation load = SceneManager.LoadSceneAsync("DedicatedServerBootstrap", LoadSceneMode.Additive);
            while (!load.isDone) yield return null;
            DedicatedServerBootstrap bootstrap = Object.FindObjectOfType<DedicatedServerBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Configuration, Is.Not.Null);

            int dataPort = AllocatePort();
            var launch = new DedicatedServerLaunchConfiguration(
                18, dataPort, AllocatePort(), "formal-credential", "SampleScene", "v1",
                new[] { new DedicatedAuthorityPawnConfiguration(100, 1, 1, "PlayerPoint 1", "connection-a") });
            var root = new GameObject("FormalDedicatedRuntimeTest");
            DedicatedServerRuntime runtime = root.AddComponent<DedicatedServerRuntime>();
            Task start = runtime.StartAsync(launch, bootstrap.Configuration, loadLevel: true);
            while (!start.IsCompleted) yield return null;
            Assert.That(start.IsFaulted, Is.False, start.Exception?.GetBaseException().ToString());

            Pawn pawn = runtime.AuthorityPawns[0];
            Assert.That(pawn.GetComponent<PawnMovementComponent>().Motor, Is.Not.Null);
            Assert.That(pawn.TryGetComponent(out PawnHeroComponent _), Is.False);
            Assert.That(pawn.TryGetComponent(out PawnCameraComponent _), Is.False);

            var channel = new ClientMovementNetworkChannel(new LiteNetClientNetworkTransport());
            OwnerReconcile? received = null;
            var spawnedEnemies = new List<EnemySpawnedEvent>();
            var snapshotsByEnemyId = new Dictionary<long, List<EnemySnapshotEvent>>();
            var enemyActions = new List<EnemyActionEvent>();
            var ownerGameplayStates = new List<OwnerGameplayStateEvent>();
            channel.OwnerReconcileReceived += response =>
                received = NetworkMessageSerializer.Deserialize<OwnerReconcileWireMessage>(response.Payload).ToValue();
            channel.EnemySpawnedReceived += response =>
                spawnedEnemies.Add(NetworkMessageSerializer.Deserialize<EnemySpawnedEvent>(response.Payload));
            channel.EnemySnapshotReceived += response =>
            {
                EnemySnapshotEvent snapshot = NetworkMessageSerializer.Deserialize<EnemySnapshotEvent>(response.Payload);
                if (!snapshotsByEnemyId.TryGetValue(snapshot.EnemyId, out List<EnemySnapshotEvent> snapshots))
                {
                    snapshots = new List<EnemySnapshotEvent>();
                    snapshotsByEnemyId.Add(snapshot.EnemyId, snapshots);
                }
                snapshots.Add(snapshot);
            };
            channel.EnemyActionReceived += response =>
                enemyActions.Add(NetworkMessageSerializer.Deserialize<EnemyActionEvent>(response.Payload));
            channel.OwnerGameplayStateReceived += response =>
                ownerGameplayStates.Add(NetworkMessageSerializer.Deserialize<OwnerGameplayStateEvent>(response.Payload));
            channel.Connect(18, $"127.0.0.1:{dataPort}", "formal-credential:100");
            for (int frame = 0; frame < 60 && !channel.IsConnected; frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(channel.IsConnected, Is.True);
            for (int frame = 0; frame < 150 &&
                 (spawnedEnemies.Count < 3 || snapshotsByEnemyId.Count < 3 ||
                  snapshotsByEnemyId.Values.Any(snapshots => snapshots.Count < 2)); frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(spawnedEnemies, Has.Count.EqualTo(3));
            Assert.That(spawnedEnemies.ConvertAll(spawned => spawned.ArchetypeId), Is.EquivalentTo(new[]
            {
                "Enemy.Pistol", "Enemy.Rifle", "Enemy.Ak"
            }));
            Assert.That(snapshotsByEnemyId, Has.Count.EqualTo(3));
            foreach (List<EnemySnapshotEvent> snapshots in snapshotsByEnemyId.Values)
                Assert.That(snapshots[snapshots.Count - 1].AuthorityServerTick, Is.GreaterThan(snapshots[0].AuthorityServerTick));
            yield return CaptureFormalFrame("patrol.png");

            for (int frame = 0; frame < 180 && snapshotsByEnemyId.Values.Any(snapshots =>
                     !snapshots.Any(snapshot => snapshot.PlanarVelocity.ToValue().ToMeters().sqrMagnitude > 0.01f)); frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            foreach (List<EnemySnapshotEvent> snapshots in snapshotsByEnemyId.Values)
            {
                bool movedTowardPossessedPawn = snapshots.Any(snapshot =>
                    snapshot.PlanarVelocity.ToValue().ToMeters().sqrMagnitude > 0.01f);
                Assert.That(movedTowardPossessedPawn, Is.True,
                    "Each archetype must receive a server-authoritative Chase motor intent for the possessed Pawn.");
            }
            yield return CaptureFormalFrame("chase.png");

            for (int frame = 0; frame < 360 && snapshotsByEnemyId.Values.Any(snapshots =>
                     !snapshots.Any(snapshot => snapshot.BrainState == EnemyBrainState.CoverHold) ||
                     !snapshots.Any(snapshot => snapshot.BrainState == EnemyBrainState.PeekFire)); frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(snapshotsByEnemyId, Has.Count.EqualTo(3));
            var coverPointIds = new HashSet<string>();
            foreach (List<EnemySnapshotEvent> snapshots in snapshotsByEnemyId.Values)
            {
                Assert.That(snapshots.Any(snapshot => snapshot.BrainState == EnemyBrainState.CoverHold), Is.True,
                    "Each enemy must hold its selected cover point through the authoritative Snapshot stream.");
                Assert.That(snapshots.Any(snapshot => snapshot.BrainState == EnemyBrainState.PeekFire), Is.True,
                    "Each enemy must enter PeekFire through the authoritative Snapshot stream.");
                EnemySnapshotEvent coverSnapshot = snapshots.First(snapshot => !string.IsNullOrWhiteSpace(snapshot.CoverPointId));
                Assert.That(coverPointIds.Add(coverSnapshot.CoverPointId), Is.True,
                    "Three enemies must not share an authored CoverPointId.");
            }
            yield return CaptureFormalFrame("cover-hold.png");
            yield return CaptureFormalFrame("peek-fire.png");

            for (int frame = 0; frame < 180 &&
                 (!enemyActions.Exists(action => action.ActionKind == EnemyActionKind.Fire) ||
                  !enemyActions.Exists(action => action.ActionKind == EnemyActionKind.Hit) ||
                  !enemyActions.Exists(action => action.ActionKind == EnemyActionKind.NoAmmo) ||
                  !ownerGameplayStates.Exists(state => state.PawnId == 100 && state.Health < 100)); frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(enemyActions.Exists(action => action.ActionKind == EnemyActionKind.Fire), Is.True,
                "Dedicated hitscan must publish a reliable Fire action.");
            Assert.That(enemyActions.Exists(action => action.ActionKind == EnemyActionKind.Hit), Is.True,
                "Only a confirmed hitscan target may publish a Hit action.");
            Assert.That(enemyActions.Exists(action => action.ActionKind == EnemyActionKind.NoAmmo), Is.True,
                "A depleted enemy magazine must publish NoAmmo exactly through the authoritative action stream.");
            Assert.That(ownerGameplayStates.Exists(state => state.PawnId == 100 && state.Health < 100 && state.VitalsRevision > 0), Is.True,
                "Only the Dedicated GameplayEffect result may advance the owner's vitals revision.");
            foreach (IGrouping<long, EnemyActionEvent> actionsByEnemy in enemyActions.GroupBy(action => action.EnemyId))
                Assert.That(actionsByEnemy.Select(action => action.ActionSequence), Is.Ordered.Ascending,
                    "Reliable enemy ActionSequence values must be strictly ordered per enemy.");

            for (int frame = 0; frame < 180 && snapshotsByEnemyId.Values.Any(snapshots =>
                     !snapshots.Any(snapshot => snapshot.BrainState == EnemyBrainState.NoAmmo)); frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(snapshotsByEnemyId.Values.Any(snapshots =>
                    snapshots.Any(snapshot => snapshot.BrainState == EnemyBrainState.NoAmmo)), Is.True,
                "An enemy with an unobstructed sight line must reach the stationary NoAmmo terminal state; enemies behind authored cover remain in patrol until cover selection is introduced.");

            channel.Send(new PawnMove(
                18, 100, 1, 1, System.Math.Max(1, runtime.FixedStepCount),
                new QuantizedInput(short.MaxValue, 0), new QuantizedView(0, 0), PawnMoveFlags.None,
                new QuantizedVector3(int.MaxValue, 0, 0)));
            for (int frame = 0; frame < 60 && !received.HasValue; frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Kind, Is.EqualTo(OwnerReconcileKind.Correction));
            Assert.That(received.Value.Reason, Is.EqualTo("PositionError"));
            Assert.That(received.Value.AuthorityState.Rotation.W, Is.Not.Zero);

            int actionCountAtNoAmmo = enemyActions.Count;
            for (int frame = 0; frame < 120; frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(enemyActions.Count, Is.EqualTo(actionCountAtNoAmmo),
                "NoAmmo must suppress further Fire actions until a future Reload feature exists.");
            channel.Dispose();
            Object.Destroy(root);
            yield return null;
            AsyncOperation unload = SceneManager.UnloadSceneAsync("DedicatedServerBootstrap");
            while (unload != null && !unload.isDone) yield return null;
            unload = SceneManager.UnloadSceneAsync("SampleScene");
            while (unload != null && !unload.isDone) yield return null;
        }

        private static IEnumerator CaptureFormalFrame(string fileName)
        {
            Camera camera = Camera.main ?? Object.FindObjectOfType<Camera>();
            Assert.That(camera, Is.Not.Null, "Formal visual capture requires the SampleScene camera.");
            camera.transform.position = new Vector3(0f, 9f, -13f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.zero - camera.transform.position, Vector3.up);
            string harnessRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
            string directory = Path.Combine(harnessRoot, ".harness", "runs", "ThreeEnemyCoverFormalAcceptance-064-20260905-formal");
            Directory.CreateDirectory(directory);
            string screenshotPath = Path.Combine(directory, fileName);
            if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
            ScreenCapture.CaptureScreenshot(screenshotPath);
            for (int frame = 0; frame < 8 && !File.Exists(screenshotPath); frame++)
                yield return new WaitForEndOfFrame();
            Assert.That(File.Exists(screenshotPath), Is.True, $"Formal screenshot was not written: {fileName}");
        }

        private static int AllocatePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
