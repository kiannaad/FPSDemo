using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public sealed class OwnerNetworkAnimationActionGateway : IDiscreteActionReplicationGateway
    {
        private readonly long pawnId;
        private readonly long possessionRevision;
        private readonly ClientNetworkSubSystem network;
        private readonly NetworkAnimationActionBridge bridge;
        private readonly Dictionary<long, Action> pendingCommitsByNonce = new Dictionary<long, Action>();
        private readonly Dictionary<long, Action> pendingCommitsBySequence = new Dictionary<long, Action>();

        public OwnerNetworkAnimationActionGateway(
            long pawnId,
            long possessionRevision,
            ClientNetworkSubSystem network,
            NetworkAnimationActionBridge bridge)
        {
            this.pawnId = pawnId;
            this.possessionRevision = possessionRevision;
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            bridge.PredictionConfirmed += OnPredictionConfirmed;
            bridge.ActionCommitted += OnActionCommitted;
        }

        public long BeginPredicted(
            DiscreteActionKind actionKind,
            string variantId,
            long equipmentInstanceId)
        {
            NetworkAnimationActionKind networkKind = (NetworkAnimationActionKind)((int)actionKind + 1);
            long nonce = bridge.BeginPredicted(networkKind, variantId, equipmentInstanceId);
            _ = SendAsync(new NetworkAnimationActionRequest
            {
                PawnId = pawnId,
                PossessionRevision = possessionRevision,
                PredictionNonce = nonce,
                ActionKind = networkKind,
                VariantId = variantId,
                EquipmentInstanceId = equipmentInstanceId
            });
            return nonce;
        }

        public void RegisterCommit(long predictionNonce, Action callback)
        {
            if (predictionNonce <= 0) throw new ArgumentOutOfRangeException(nameof(predictionNonce));
            pendingCommitsByNonce[predictionNonce] = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        private void OnPredictionConfirmed(long predictionNonce, long actionSequence)
        {
            if (!pendingCommitsByNonce.TryGetValue(predictionNonce, out Action callback)) return;
            pendingCommitsByNonce.Remove(predictionNonce);
            pendingCommitsBySequence[actionSequence] = callback;
        }

        private void OnActionCommitted(long actionSequence)
        {
            if (!pendingCommitsBySequence.TryGetValue(actionSequence, out Action callback)) return;
            pendingCommitsBySequence.Remove(actionSequence);
            callback();
        }

        private async System.Threading.Tasks.Task SendAsync(NetworkAnimationActionRequest request)
        {
            try
            {
                await network.SendAnimationActionRequestAsync(request);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Network][042] PredictedActionRejected PredictionNonce={request.PredictionNonce} Reason={exception.Message}");
            }
        }
    }

    public sealed class ExistingLocalPredictionPresenter : INetworkAnimationActionPresenter
    {
        public bool TryPlay(NetworkAnimationActionStarted action, float elapsedSeconds, out object playbackHandle)
        {
            playbackHandle = action.ActionSequence == 0 ? new object() : null;
            return playbackHandle != null;
        }

        public void Stop(object playbackHandle)
        {
        }
    }
}
