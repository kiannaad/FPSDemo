using System.Collections;
using System.Collections.Generic;
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
        [UnityTest]
        public IEnumerator StartAsync_CreatesAuthorityPawnsAndAdvancesSixtyHertzPhysics()
        {
            if (World.Current != null)
            {
                Task shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted) yield return null;
            }

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
            if (World.Current != null)
            {
                Task shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted) yield return null;
            }

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
            if (World.Current != null)
            {
                Task shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted) yield return null;
            }

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
            channel.OwnerReconcileReceived += response =>
                received = NetworkMessageSerializer.Deserialize<OwnerReconcileWireMessage>(response.Payload).ToValue();
            channel.Connect(18, $"127.0.0.1:{dataPort}", "formal-credential:100");
            for (int frame = 0; frame < 60 && !channel.IsConnected; frame++)
            {
                channel.PollEvents();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(channel.IsConnected, Is.True);
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
            channel.Dispose();
            Object.Destroy(root);
            yield return null;
            AsyncOperation unload = SceneManager.UnloadSceneAsync("DedicatedServerBootstrap");
            while (unload != null && !unload.isDone) yield return null;
            unload = SceneManager.UnloadSceneAsync("SampleScene");
            while (unload != null && !unload.isDone) yield return null;
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
