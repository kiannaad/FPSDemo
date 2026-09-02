using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public interface INetworkAnimationActionPresenter
    {
        bool TryPlay(NetworkAnimationActionStarted action, float elapsedSeconds, out object playbackHandle);
        void Stop(object playbackHandle);
    }

    public sealed class NetworkAnimationActionBridge
    {
        private readonly long pawnId;
        private readonly long possessionRevision;
        private readonly NetworkTickClock clock;
        private readonly INetworkAnimationActionPresenter presenter;
        private readonly Dictionary<long, object> activeHandles = new Dictionary<long, object>();
        private readonly HashSet<long> observedSequences = new HashSet<long>();
        private readonly HashSet<long> terminalSequences = new HashSet<long>();
        private readonly Dictionary<long, object> predictedHandles = new Dictionary<long, object>();
        private readonly HashSet<long> rejectedPredictionNonces = new HashSet<long>();
        private long nextPredictionNonce;

        public NetworkAnimationActionBridge(
            long pawnId,
            long possessionRevision,
            NetworkTickClock clock,
            INetworkAnimationActionPresenter presenter)
        {
            if (pawnId <= 0) throw new ArgumentOutOfRangeException(nameof(pawnId));
            if (possessionRevision < 0) throw new ArgumentOutOfRangeException(nameof(possessionRevision));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            this.pawnId = pawnId;
            this.possessionRevision = possessionRevision;
        }

        public int PlayedActionCount { get; private set; }
        public int RejectedActionCount { get; private set; }
        public int PresentationUnavailableCount { get; private set; }
        public int ConfirmedPredictionCount { get; private set; }
        public long LastElapsedTicks { get; private set; }
        public int ActiveActionCount => activeHandles.Count;
        public event Action<long, long> PredictionConfirmed;
        public event Action<NetworkAnimationActionTerminal> ActionCommitted;

        public long BeginPredicted(
            NetworkAnimationActionKind actionKind,
            string variantId,
            long equipmentInstanceId)
        {
            long nonce = checked(++nextPredictionNonce);
            var predicted = new NetworkAnimationActionStarted
            {
                PawnId = pawnId,
                PossessionRevision = possessionRevision,
                PredictionNonce = nonce,
                ActionSequence = 0,
                ServerStartTick = clock.EstimatedServerTick,
                DurationTicks = int.MaxValue,
                ActionKind = actionKind,
                VariantId = variantId,
                EquipmentInstanceId = equipmentInstanceId
            };
            return nonce;
        }

        public bool RegisterPredictedPlayback(long predictionNonce, object playbackHandle)
        {
            if (predictionNonce <= 0 || playbackHandle == null) return false;
            if (rejectedPredictionNonces.Remove(predictionNonce))
            {
                presenter.Stop(playbackHandle);
                return false;
            }

            if (predictedHandles.ContainsKey(predictionNonce)) return false;
            predictedHandles.Add(predictionNonce, playbackHandle);
            return true;
        }

        public bool RejectPredicted(long predictionNonce)
        {
            if (predictionNonce <= 0) return false;
            rejectedPredictionNonces.Add(predictionNonce);
            if (!predictedHandles.TryGetValue(predictionNonce, out object handle)) return false;
            predictedHandles.Remove(predictionNonce);
            presenter.Stop(handle);
            return true;
        }

        public bool ApplyStarted(NetworkAnimationActionStarted action)
        {
            if (!IsValid(action) || terminalSequences.Contains(action.ActionSequence))
            {
                RejectedActionCount++;
                return false;
            }

            if (!observedSequences.Add(action.ActionSequence)) return false;
            long elapsedTicks = clock.ElapsedTicksSince(action.ServerStartTick);
            LastElapsedTicks = elapsedTicks;
            if (elapsedTicks >= action.DurationTicks)
            {
                RejectedActionCount++;
                terminalSequences.Add(action.ActionSequence);
                return false;
            }

            if (action.PredictionNonce > 0 && predictedHandles.TryGetValue(
                    action.PredictionNonce,
                    out object predictedHandle))
            {
                predictedHandles.Remove(action.PredictionNonce);
                activeHandles.Add(action.ActionSequence, predictedHandle);
                ConfirmedPredictionCount++;
                PredictionConfirmed?.Invoke(action.PredictionNonce, action.ActionSequence);
                Debug.Log($"[Network][042] PredictedActionConfirmed PawnId={pawnId} PredictionNonce={action.PredictionNonce} ActionSequence={action.ActionSequence}");
                return true;
            }

            if (action.PredictionNonce > 0 && rejectedPredictionNonces.Contains(action.PredictionNonce))
            {
                RejectedActionCount++;
                terminalSequences.Add(action.ActionSequence);
                return false;
            }

            float elapsedSeconds = clock.TicksToSeconds(elapsedTicks);
            if (!presenter.TryPlay(action, elapsedSeconds, out object handle) || handle == null)
            {
                PresentationUnavailableCount++;
                Debug.Log($"[Network][041] AnimationPresentationUnavailable PawnId={pawnId} ActionSequence={action.ActionSequence} VariantId={action.VariantId}");
                return false;
            }

            activeHandles.Add(action.ActionSequence, handle);
            PlayedActionCount++;
            Debug.Log($"[Network][041] AnimationActionStarted PawnId={pawnId} ActionSequence={action.ActionSequence} ElapsedTicks={elapsedTicks}");
            return true;
        }

        public bool ApplyTerminal(NetworkAnimationActionTerminal terminal)
        {
            if (terminal == null || terminal.PawnId != pawnId ||
                terminal.PossessionRevision != possessionRevision || terminal.ActionSequence <= 0 ||
                !Enum.IsDefined(typeof(NetworkAnimationActionTerminalKind), terminal.TerminalKind))
            {
                RejectedActionCount++;
                return false;
            }

            observedSequences.Add(terminal.ActionSequence);
            if (terminal.TerminalKind == NetworkAnimationActionTerminalKind.Committed)
            {
                ActionCommitted?.Invoke(terminal);
                Debug.Log($"[Network][042] AnimationActionCommitted PawnId={pawnId} ActionSequence={terminal.ActionSequence}");
                return activeHandles.ContainsKey(terminal.ActionSequence);
            }

            terminalSequences.Add(terminal.ActionSequence);
            if (!activeHandles.TryGetValue(terminal.ActionSequence, out object handle)) return false;
            presenter.Stop(handle);
            activeHandles.Remove(terminal.ActionSequence);
            Debug.Log($"[Network][041] AnimationActionTerminal PawnId={pawnId} ActionSequence={terminal.ActionSequence} Kind={terminal.TerminalKind}");
            return true;
        }

        private bool IsValid(NetworkAnimationActionStarted action) =>
            action != null && action.PawnId == pawnId &&
            action.PossessionRevision == possessionRevision && action.ActionSequence > 0 &&
            action.ServerStartTick >= 0 && action.DurationTicks > 0 &&
            Enum.IsDefined(typeof(NetworkAnimationActionKind), action.ActionKind);
    }
}
