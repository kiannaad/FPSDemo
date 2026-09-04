using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using MessagePack;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedDataServer : IDisposable
    {
        private readonly EventBasedLiteNetListener listener = new EventBasedLiteNetListener();
        private readonly DedicatedDataCredentialResolver credentialResolver;
        private readonly Dictionary<int, DedicatedDataIdentity> identitiesByPeer =
            new Dictionary<int, DedicatedDataIdentity>();
        private readonly Dictionary<long, LiteNetPeer> peersByPawn = new Dictionary<long, LiteNetPeer>();
        private readonly HashSet<long> pendingConnectedPawnIds = new HashSet<long>();
        private LiteNetManager manager;

        public DedicatedDataServer(DedicatedServerLaunchConfiguration launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            credentialResolver = new DedicatedDataCredentialResolver(launch.Credential, launch.AuthorityPawns);
            listener.ConnectionRequestEvent += OnConnectionRequest;
            listener.PeerDisconnectedEvent += OnPeerDisconnected;
            listener.NetworkReceiveEvent += OnReceive;
            manager = new LiteNetManager(listener);
            if (!manager.Start(launch.DataPort))
            {
                manager.Stop();
                manager = null;
                throw new InvalidOperationException($"Unable to bind Dedicated data port {launch.DataPort}.");
            }
        }

        public event Action<DedicatedDataIdentity, PawnMove> MoveReceived;
        public event Action<long> PawnConnected;

        public void PollEvents()
        {
            manager?.PollEvents();
            foreach (long pawnId in pendingConnectedPawnIds.ToArray())
            {
                pendingConnectedPawnIds.Remove(pawnId);
                PawnConnected?.Invoke(pawnId);
            }
        }

        public void SendOwnerReconcile(long pawnId, long matchId, OwnerReconcile reconcile)
        {
            if (!peersByPawn.TryGetValue(pawnId, out LiteNetPeer peer)) return;
            var message = new OwnerReconcileWireMessage
            {
                Kind = reconcile.Kind,
                AckSequence = reconcile.AckSequence,
                AuthorityState = AuthorityStateWireMessage.FromValue(reconcile.AuthorityState),
                Reason = reconcile.Reason
            };
            byte[] payload = MessagePackSerializer.Serialize(message);
            byte[] packet = NetworkPacketCodec.Encode(new NetworkPacketHeader(
                NetworkPacketCodec.ProtocolVersion,
                NetworkMessageId.OwnerReconcile,
                NetworkPacketFlags.Response,
                0,
                matchId), payload);
            peer.Send(packet, DeliveryMethod.Sequenced);
        }

        public void BroadcastAuthoritySnapshot(AuthoritySnapshot snapshot)
        {
            byte[] payload = MessagePackSerializer.Serialize(AuthoritySnapshotWireMessage.FromValue(snapshot));
            byte[] packet = NetworkPacketCodec.Encode(new NetworkPacketHeader(
                NetworkPacketCodec.ProtocolVersion,
                NetworkMessageId.AuthoritySnapshot,
                NetworkPacketFlags.Response,
                0,
                snapshot.MatchId), payload);
            foreach (LiteNetPeer peer in peersByPawn.Values)
            {
                if (peer.ConnectionState == ConnectionState.Connected)
                    peer.Send(packet, DeliveryMethod.Sequenced);
            }
        }

        public void SendEnemySpawn(long pawnId, long matchId, EnemySpawnedEvent spawned)
        {
            if (!peersByPawn.TryGetValue(pawnId, out LiteNetPeer peer)) return;
            SendEnemyMessage(peer, matchId, NetworkMessageId.EnemySpawned, spawned, DeliveryMethod.ReliableOrdered);
        }

        public void SendEnemySnapshot(long pawnId, long matchId, EnemySnapshotEvent snapshot)
        {
            if (!peersByPawn.TryGetValue(pawnId, out LiteNetPeer peer)) return;
            SendEnemyMessage(peer, matchId, NetworkMessageId.EnemySnapshot, snapshot, DeliveryMethod.Sequenced);
        }

        public void BroadcastEnemySnapshot(long matchId, EnemySnapshotEvent snapshot)
        {
            foreach (LiteNetPeer peer in peersByPawn.Values)
                SendEnemyMessage(peer, matchId, NetworkMessageId.EnemySnapshot, snapshot, DeliveryMethod.Sequenced);
        }

        public void BroadcastEnemyAction(long matchId, EnemyActionEvent action)
        {
            foreach (LiteNetPeer peer in peersByPawn.Values)
                SendEnemyMessage(peer, matchId, NetworkMessageId.EnemyAction, action, DeliveryMethod.ReliableOrdered);
        }

        public void SendOwnerGameplayState(long pawnId, long matchId, OwnerGameplayStateEvent state)
        {
            if (!peersByPawn.TryGetValue(pawnId, out LiteNetPeer peer)) return;
            SendEnemyMessage(peer, matchId, NetworkMessageId.OwnerGameplayState, state, DeliveryMethod.ReliableOrdered);
        }

        public void Dispose()
        {
            manager?.Stop();
            manager = null;
            identitiesByPeer.Clear();
            peersByPawn.Clear();
            pendingConnectedPawnIds.Clear();
        }

        private void OnConnectionRequest(LiteConnectionRequest request)
        {
            string credential;
            try
            {
                credential = request.Data.GetString();
            }
            catch
            {
                request.Reject();
                return;
            }

            if (!credentialResolver.TryResolve(credential, out DedicatedDataIdentity identity))
            {
                Debug.LogWarning("[DedicatedServer][038] DataConnectionRejected Reason=Credential");
                request.Reject();
                return;
            }

            LiteNetPeer peer = request.Accept();
            if (peer != null)
            {
                identitiesByPeer[peer.Id] = identity;
                peersByPawn[identity.PawnId] = peer;
                pendingConnectedPawnIds.Add(identity.PawnId);
                Debug.Log($"[DedicatedServer][038] DataConnectionAccepted PawnId={identity.PawnId} ConnectionId={identity.ConnectionId}");
            }
        }

        private static void SendEnemyMessage<T>(
            LiteNetPeer peer,
            long matchId,
            NetworkMessageId messageId,
            T message,
            DeliveryMethod delivery)
        {
            if (peer == null) return;
            byte[] payload = MessagePackSerializer.Serialize(message);
            byte[] packet = NetworkPacketCodec.Encode(new NetworkPacketHeader(
                NetworkPacketCodec.ProtocolVersion,
                messageId,
                NetworkPacketFlags.Response,
                0,
                matchId), payload);
            peer.Send(packet, delivery);
        }

        private void OnPeerDisconnected(LiteNetPeer peer, DisconnectInfo _)
        {
            if (!identitiesByPeer.TryGetValue(peer.Id, out DedicatedDataIdentity identity)) return;
            identitiesByPeer.Remove(peer.Id);
            if (peersByPawn.TryGetValue(identity.PawnId, out LiteNetPeer current) && current == peer)
                peersByPawn.Remove(identity.PawnId);
            pendingConnectedPawnIds.Remove(identity.PawnId);
        }

        private void OnReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                if (deliveryMethod != DeliveryMethod.Sequenced)
                {
                    Debug.LogWarning($"[DedicatedServer][038] MoveRejected Reason=Delivery Delivery={deliveryMethod}");
                    return;
                }
                if (!identitiesByPeer.TryGetValue(peer.Id, out DedicatedDataIdentity identity))
                {
                    Debug.LogWarning("[DedicatedServer][038] MoveRejected Reason=PeerIdentity");
                    return;
                }
                byte[] packet = reader.GetRemainingBytes();
                if (!NetworkPacketCodec.TryDecode(packet, out NetworkPacketHeader header, out byte[] payload) ||
                    header.MessageId != NetworkMessageId.PawnMove)
                {
                    Debug.LogWarning($"[DedicatedServer][038] MoveRejected Reason=Packet Bytes={packet.Length}");
                    return;
                }
                PawnMove move = MessagePackSerializer.Deserialize<PawnMoveWireMessage>(payload).ToMove();
                MoveReceived?.Invoke(identity, move);
            }
            finally
            {
                reader.Recycle();
            }
        }
    }
}
