using System;

namespace CGame
{
    public sealed class PawnInitContext
    {
        public PawnInitContext(Pawn pawn, PawnData pawnData)
        {
            Pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            PawnData = pawnData ?? throw new ArgumentNullException(nameof(pawnData));
        }

        public Pawn Pawn { get; }

        public PawnData PawnData { get; }
    }
}
