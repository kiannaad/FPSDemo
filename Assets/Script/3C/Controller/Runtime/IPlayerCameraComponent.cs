using System;

namespace CGame
{
    public interface IPlayerCameraComponent : IDisposable
    {
        bool IsDisposed { get; }

        Pawn BoundPawn { get; }

        PawnBindingReceipt BindPawn(Pawn pawn);
    }
}
