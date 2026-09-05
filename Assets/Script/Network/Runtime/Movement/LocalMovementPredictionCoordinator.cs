using System;
using CGame;
using UnityEngine;

namespace CGame.Network
{
    public sealed class LocalMovementPredictionCoordinator
    {
        private readonly ClientNetworkSubSystem network;
        private readonly LocalMovePredictionBuffer prediction = new LocalMovePredictionBuffer(NetworkPawnRole.LocalAutonomous);
        private Pawn pawn;
        private NetworkPawnBinding binding;
        private LocalPawnMoveReplayTarget replayTarget;
        private long matchId;
        private long nextMoveSequence;
        private bool configurationLogged;
        private bool waitingForMovementConnectionLogged;

        public LocalMovementPredictionCoordinator(ClientNetworkSubSystem network)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public int SavedMoveCount => prediction.SavedMoves.Count;
        public int MoveCreatedCount { get; private set; }
        public int ReplayCompletedCount { get; private set; }
        public OwnerReconcileKind? LastReconcileKind { get; private set; }
        public string Diagnostic => $"MatchId={matchId} Pawn={pawn != null} Binding={binding != null} DataConnected={network.IsMovementConnected} Moves={MoveCreatedCount} Reconcile={LastReconcileKind} Replays={ReplayCompletedCount}";

        public void Configure(Pawn nextPawn, NetworkPawnBinding nextBinding)
        {
            pawn = nextPawn ?? throw new ArgumentNullException(nameof(nextPawn));
            binding = nextBinding ?? throw new ArgumentNullException(nameof(nextBinding));
            replayTarget = new LocalPawnMoveReplayTarget(pawn);
            configurationLogged = true;
            Debug.Log($"[Network][038] PredictionConfigured PawnId={binding.PawnId} Revision={binding.PossessionRevision}");
        }

        public void SetMatchId(long nextMatchId)
        {
            matchId = nextMatchId;
            Debug.Log($"[Network][038] PredictionMatchBound MatchId={matchId} Configured={configurationLogged}");
        }

        public void OnFixedStepCompleted(long clientTick)
        {
            if (matchId <= 0 || pawn == null || binding == null || !network.IsMovementConnected)
            {
                if (!waitingForMovementConnectionLogged)
                {
                    waitingForMovementConnectionLogged = true;
                    Debug.Log($"[Network][038] PredictionWaiting MatchId={matchId} Pawn={pawn != null} Binding={binding != null} DataConnected={network.IsMovementConnected}");
                }
                return;
            }
            Vector3 input = pawn.PeekingMovementInput();
            Vector3 view = pawn.ControlRotation.eulerAngles;
            CharacterMovementCommand command = pawn.LastConsumedMovementCommand;
            PawnMoveFlags flags = PawnMoveFlags.None;
            if (command.JumpRequested) flags |= PawnMoveFlags.Jump;
            if (command.SprintRequested) flags |= PawnMoveFlags.Sprint;
            var move = new PawnMove(
                matchId,
                binding.PawnId,
                binding.PossessionRevision,
                ++nextMoveSequence,
                clientTick,
                QuantizedInput.FromVector2(new Vector2(input.x, input.z)),
                QuantizedView.FromDegrees(view.y, Mathf.DeltaAngle(0f, view.x)),
                flags,
                QuantizedVector3.FromMeters(pawn.Transform.position));
            prediction.Add(move);
            MoveCreatedCount++;
            if (MoveCreatedCount == 1) Debug.Log($"[Network][038] MoveCreated Sequence={move.Sequence} ClientTick={clientTick}");
            if (flags != PawnMoveFlags.None)
                Debug.Log($"[Network][043] InputMoveTrace MatchId={matchId} PawnId={binding.PawnId} Sequence={move.Sequence} ClientTick={clientTick} Flags={flags}");
            network.SendPawnMove(move);
        }

        public void OnReconcileReceived(OwnerReconcile reconcile)
        {
            LastReconcileKind = reconcile.Kind;
            if (reconcile.Kind == OwnerReconcileKind.Ack)
            {
                prediction.Acknowledge(reconcile.AckSequence);
                return;
            }

            if (replayTarget == null) return;
            LocalCorrectionResult result = prediction.Correct(reconcile.AckSequence, reconcile.AuthorityState, replayTarget);
            ReplayCompletedCount++;
            Debug.Log($"[Network][038] ReplayCompleted Sequence={reconcile.AckSequence} HistoryFound={result.HistoryFound} Replayed={result.ReplayedMoveCount}");
        }

        public void Clear()
        {
            pawn = null;
            binding = null;
            replayTarget = null;
            matchId = 0;
            nextMoveSequence = 0;
            configurationLogged = false;
            waitingForMovementConnectionLogged = false;
            prediction.Clear();
        }
    }
}
