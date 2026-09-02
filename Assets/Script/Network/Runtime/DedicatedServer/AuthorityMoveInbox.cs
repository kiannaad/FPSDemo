using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public readonly struct AcceptedAuthorityMove
    {
        public AcceptedAuthorityMove(DedicatedDataIdentity identity, PawnMove move)
        {
            Identity = identity;
            Move = move;
        }

        public DedicatedDataIdentity Identity { get; }
        public PawnMove Move { get; }
    }

    public sealed class AuthorityMoveInbox
    {
        private readonly Queue<AcceptedAuthorityMove> moves = new Queue<AcceptedAuthorityMove>();

        public int Count => moves.Count;

        public int GetSimulationStepCount(int targetQueuedMoves, int maxSimulationSteps)
        {
            if (targetQueuedMoves < 0)
                throw new ArgumentOutOfRangeException(nameof(targetQueuedMoves));
            if (maxSimulationSteps < 1)
                throw new ArgumentOutOfRangeException(nameof(maxSimulationSteps));

            int requiredSteps = Math.Max(1, Count - targetQueuedMoves + 1);
            return Math.Min(requiredSteps, maxSimulationSteps);
        }

        public void Enqueue(DedicatedDataIdentity identity, PawnMove move)
        {
            if (move.PawnId != identity.PawnId)
                throw new ArgumentException("Move PawnId must match the authenticated data identity.", nameof(move));
            moves.Enqueue(new AcceptedAuthorityMove(identity, move));
        }

        public bool TryDequeue(out AcceptedAuthorityMove move)
        {
            if (moves.Count == 0)
            {
                move = default;
                return false;
            }

            move = moves.Dequeue();
            return true;
        }
    }
}
