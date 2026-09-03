using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Network.Tests
{
    public sealed class DedicatedTargetIdentityMainlineTests
    {
        [UnityTest]
        [Category("Network047")]
        public IEnumerator RoomReady_StartsDedicatedServerAfterTargetRosterIsVerified()
        {
            ClientNetworkDefinition definition = ScriptableObject.CreateInstance<ClientNetworkDefinition>();
            Network047Configuration configuration = ScriptableObject.CreateInstance<Network047Configuration>();
            configuration.Configure(definition);
            World world = World.Create(configuration);
            using var secondClient = new NetworkRpcClient(new LiteNetClientNetworkTransport());
            try
            {
                world.InitializeAsync().GetAwaiter().GetResult();
                world.StartPlay();
                ClientNetworkSubSystem firstClient = world.GetSubSystem<ClientNetworkSubSystem>();
                TargetStateSnapshotMessage targetSnapshot = null;
                firstClient.TargetStateSnapshotReceived += snapshot => targetSnapshot = snapshot;
                yield return TickUntil(world, secondClient, () => firstClient.IsHelloComplete, 10f, "first client hello");

                Task<CreateRoomResponse> createTask = firstClient.CreateRoomAsync("network-047");
                yield return TickUntil(world, secondClient, () => createTask.IsCompleted, 10f, "room creation");
                Assert.That(createTask.IsFaulted, Is.False);

                secondClient.Connect("127.0.0.1", 29000, "fps-v1");
                yield return TickUntil(world, secondClient, () => secondClient.IsConnected, 10f, "second client transport connection");
                Task<NetworkRpcResponse> joinTask = secondClient.RequestAsync(
                    NetworkMessageId.JoinRoomRequest,
                    NetworkMessageSerializer.Serialize(new JoinRoomRequest { RoomId = createTask.Result.RoomId }),
                    TimeSpan.FromSeconds(10));
                yield return TickUntil(world, secondClient, () => joinTask.IsCompleted, 10f, "room join");
                Assert.That(joinTask.IsFaulted, Is.False);

                Task<SetReadyResponse> firstReadyTask = firstClient.SetReadyAsync(createTask.Result.RoomId, true);
                yield return TickUntil(world, secondClient, () => firstReadyTask.IsCompleted, 10f, "first ready");
                Assert.That(firstReadyTask.IsFaulted, Is.False);

                Task<NetworkRpcResponse> secondReadyTask = secondClient.RequestAsync(
                    NetworkMessageId.SetReadyRequest,
                    NetworkMessageSerializer.Serialize(new SetReadyRequest { RoomId = createTask.Result.RoomId, IsReady = true }),
                    TimeSpan.FromSeconds(40));
                yield return TickUntil(world, secondClient, () => secondReadyTask.IsCompleted, 45f, "second ready and dedicated startup");
                Assert.That(secondReadyTask.IsFaulted, Is.False);

                yield return TickUntil(world, secondClient, () =>
                    firstClient.ClientWorld.Pawns.Count == 2 &&
                    firstClient.ClientWorld.Pawns.Count(pawn => pawn.IsLocallyControlled) == 1, 15f,
                    () => $"client Pawn replication (MatchId={firstClient.ClientWorld.MatchId}, Pawns={firstClient.ClientWorld.Pawns.Count}, ControlledPawnId={firstClient.ClientWorld.ControlledPawnId})");

                yield return TickUntil(world, secondClient, () => targetSnapshot?.Targets?.Length == 3, 15f,
                    () => $"initial target snapshot (Count={targetSnapshot?.Targets?.Length ?? 0})");
                Assert.That(targetSnapshot.Targets.Select(target => target.TargetId), Is.EquivalentTo(new[]
                {
                    "EnemyPoint 1", "EnemyPoint 2", "EnemyPoint 3"
                }));
                Assert.That(targetSnapshot.Targets.All(target => target.Health == 60f && target.MaxHealth == 60f && !target.IsDead && target.Revision == 0), Is.True);
            }
            finally
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(configuration);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static IEnumerator TickUntil(
            World world,
            NetworkRpcClient secondClient,
            Func<bool> condition,
            float timeoutSeconds,
            string stage)
        {
            yield return TickUntil(world, secondClient, condition, timeoutSeconds, () => stage);
        }

        private static IEnumerator TickUntil(
            World world,
            NetworkRpcClient secondClient,
            Func<bool> condition,
            float timeoutSeconds,
            Func<string> stage)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                world.UpdateTick(Time.deltaTime);
                world.FixedTick(Time.fixedDeltaTime);
                world.LateTick(Time.deltaTime);
                secondClient.Tick();
                yield return null;
            }

            Assert.That(condition(), Is.True, $"The network condition did not complete before the deadline: {stage()}.");
        }

        private sealed class Network047Configuration : WorldConfiguration
        {
            private ClientNetworkDefinition definition;

            public void Configure(ClientNetworkDefinition definition)
            {
                this.definition = definition;
            }

            public override System.Collections.Generic.IReadOnlyList<WorldSubSystem> CreateWorldSubSystems()
            {
                return new WorldSubSystem[] { new ClientNetworkSubSystem(definition) };
            }
        }
    }
}
