using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Network.Tests
{
    public sealed class ClientNetworkPlayModeTests
    {
        [Test]
        public void ClientWorld_AppliesPossessionAfterDelayedPawnSpawn()
        {
            var clientWorld = new ClientWorld();
            clientWorld.SetLocalPlayer(7);
            clientWorld.OnPossessionChanged(new PossessionChangedEvent
            {
                PlayerId = 7,
                PawnId = 99,
                PossessionRevision = 1
            });
            clientWorld.OnPawnSpawned(new PawnSpawnedEvent
            {
                PawnId = 99,
                OwnerPlayerId = 7,
                SpawnPointId = "spawn-a"
            });

            Assert.That(clientWorld.ControlledPawnId, Is.EqualTo(99));
            Assert.That(clientWorld.Pawns.Single().IsLocallyControlled, Is.True);
        }

        [UnityTest]
        public IEnumerator WorldClient_CompletesHelloAgainstIndependentServer()
        {
            ClientNetworkDefinition definition = ScriptableObject.CreateInstance<ClientNetworkDefinition>();
            NetworkTestConfiguration configuration = ScriptableObject.CreateInstance<NetworkTestConfiguration>();
            configuration.Configure(definition);
            World world = World.Create(configuration);
            try
            {
                world.InitializeAsync().GetAwaiter().GetResult();
                world.StartPlay();

                var timeout = new WaitForSecondsRealtime(6f);
                ClientNetworkSubSystem network = world.GetSubSystem<ClientNetworkSubSystem>();
                while (!network.IsHelloComplete && string.IsNullOrEmpty(network.Failure))
                {
                    world.UpdateTick(Time.deltaTime);
                    yield return null;
                    if (timeout.keepWaiting == false) break;
                }

                Assert.That(network.Failure, Is.Empty);
                Assert.That(network.IsHelloComplete, Is.True);
                Assert.That(network.HelloResponse, Is.Not.Null);
                Assert.That(network.HelloResponse.ProtocolVersion, Is.EqualTo(NetworkPacketCodec.ProtocolVersion));
            }
            finally
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(configuration);
                Object.DestroyImmediate(definition);
            }
        }

        [UnityTest]
        public IEnumerator WorldClient_CreateJoinReady_AppliesAuthoritativeOwnerPossession()
        {
            ClientNetworkDefinition definition = ScriptableObject.CreateInstance<ClientNetworkDefinition>();
            NetworkTestConfiguration configuration = ScriptableObject.CreateInstance<NetworkTestConfiguration>();
            configuration.Configure(definition);
            World world = World.Create(configuration);
            using var secondClient = new NetworkRpcClient(new LiteNetClientNetworkTransport());
            try
            {
                world.InitializeAsync().GetAwaiter().GetResult();
                world.StartPlay();
                ClientNetworkSubSystem firstClient = world.GetSubSystem<ClientNetworkSubSystem>();
                yield return TickUntil(world, secondClient, () => firstClient.IsHelloComplete, 6f);

                Task<CreateRoomResponse> createTask = firstClient.CreateRoomAsync("room");
                yield return TickUntil(world, secondClient, () => createTask.IsCompleted, 6f);
                Assert.That(createTask.IsFaulted, Is.False);
                string roomId = createTask.Result.RoomId;

                secondClient.Connect("127.0.0.1", 29000, "fps-v1");
                yield return TickUntil(world, secondClient, () => secondClient.IsConnected, 6f);
                Task<NetworkRpcResponse> joinTask = secondClient.RequestAsync(
                    NetworkMessageId.JoinRoomRequest,
                    NetworkMessageSerializer.Serialize(new JoinRoomRequest { RoomId = roomId }),
                    System.TimeSpan.FromSeconds(5));
                yield return TickUntil(world, secondClient, () => joinTask.IsCompleted, 6f);
                Assert.That(joinTask.IsFaulted, Is.False);

                Task<SetReadyResponse> firstReadyTask = firstClient.SetReadyAsync(roomId, true);
                yield return TickUntil(world, secondClient, () => firstReadyTask.IsCompleted, 6f);
                Task<NetworkRpcResponse> secondReadyTask = secondClient.RequestAsync(
                    NetworkMessageId.SetReadyRequest,
                    NetworkMessageSerializer.Serialize(new SetReadyRequest { RoomId = roomId, IsReady = true }),
                    System.TimeSpan.FromSeconds(5));
                yield return TickUntil(world, secondClient, () => secondReadyTask.IsCompleted, 6f);
                Assert.That(secondReadyTask.IsFaulted, Is.False);
                yield return TickUntil(world, secondClient, () =>
                    firstClient.ClientWorld.Pawns.Count == 2 && firstClient.ClientWorld.ControlledPawnId != 0, 6f);

                Assert.That(firstClient.ClientWorld.Pawns.Count, Is.EqualTo(2));
                Assert.That(firstClient.ClientWorld.Pawns.Count(pawn => pawn.IsLocallyControlled), Is.EqualTo(1));
                Assert.That(firstClient.ClientWorld.ControlledPawnId, Is.Not.EqualTo(0));
            }
            finally
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(configuration);
                Object.DestroyImmediate(definition);
            }
        }

        private static IEnumerator TickUntil(
            World world,
            NetworkRpcClient secondClient,
            System.Func<bool> condition,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                world.UpdateTick(Time.deltaTime);
                secondClient.Tick();
                yield return null;
            }

            Assert.That(condition(), Is.True, "The network condition did not complete before the deadline.");
        }

        private sealed class NetworkTestConfiguration : WorldConfiguration
        {
            private ClientNetworkDefinition definition;

            public void Configure(ClientNetworkDefinition definition)
            {
                this.definition = definition;
            }

            public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems()
            {
                return new WorldSubSystem[] { new ClientNetworkSubSystem(definition) };
            }
        }
    }
}
