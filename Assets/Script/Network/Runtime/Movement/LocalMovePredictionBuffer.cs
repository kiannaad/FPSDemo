using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public interface ILocalMoveReplayTarget
    {
        void ApplyAuthorityState(AuthorityState state);
        void Replay(PawnMove move);
    }

    public readonly struct LocalCorrectionResult
    {
        public LocalCorrectionResult(bool historyFound, int replayedMoveCount)
        {
            HistoryFound = historyFound;
            ReplayedMoveCount = replayedMoveCount;
        }

        public bool HistoryFound { get; }
        public int ReplayedMoveCount { get; }
    }

    public sealed class LocalMovePredictionBuffer
    {
        private readonly List<PawnMove> savedMoves = new List<PawnMove>();

        public LocalMovePredictionBuffer(NetworkPawnRole role)
        {
            if (role != NetworkPawnRole.LocalAutonomous)
            {
                throw new InvalidOperationException("Only LocalAutonomous owns SavedMove history.");
            }
        }

        public IReadOnlyList<PawnMove> SavedMoves => savedMoves;

        public void Add(PawnMove move)
        {
            if (move.Sequence <= 0) throw new ArgumentOutOfRangeException(nameof(move));
            if (savedMoves.Count > 0 && move.Sequence != savedMoves[savedMoves.Count - 1].Sequence + 1)
            {
                throw new InvalidOperationException("SavedMove Sequence must increase by exactly one.");
            }

            savedMoves.Add(move);
        }

        public void Acknowledge(long ackSequence)
        {
            savedMoves.RemoveAll(move => move.Sequence <= ackSequence);
        }

        public void Clear()
        {
            savedMoves.Clear();
        }

        public LocalCorrectionResult Correct(
            long correctedSequence,
            AuthorityState authorityState,
            ILocalMoveReplayTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.ApplyAuthorityState(authorityState);
            bool historyFound = savedMoves.Exists(move => move.Sequence == correctedSequence);
            if (!historyFound)
            {
                savedMoves.Clear();
                return new LocalCorrectionResult(false, 0);
            }

            Acknowledge(correctedSequence);
            foreach (PawnMove move in savedMoves)
            {
                target.Replay(move);
            }

            return new LocalCorrectionResult(true, savedMoves.Count);
        }
    }
}
