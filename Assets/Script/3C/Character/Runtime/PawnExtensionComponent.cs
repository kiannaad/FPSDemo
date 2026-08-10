using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class PawnExtensionComponent
    {
        private readonly List<IPawnInitStateParticipant> participants =
            new List<IPawnInitStateParticipant>();
        private bool initializationCheckRequested;
        private bool participantsShutdown;

        public PawnExtensionComponent(Pawn pawn, PawnData pawnData)
        {
            Context = new PawnInitContext(pawn, pawnData);
        }

        public PawnInitContext Context { get; }

        public PawnInitState State { get; private set; } = PawnInitState.Spawned;

        public Exception Fault { get; private set; }

        public bool IsInitializationCheckRequested => initializationCheckRequested;

        public event Action InitializationRequested;

        public event Action<Exception> InitializationFaulted;

        public void RegisterParticipant(IPawnInitStateParticipant participant)
        {
            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }

            if (State != PawnInitState.Spawned || participants.Contains(participant))
            {
                throw new InvalidOperationException("Pawn participants must register exactly once while Spawned.");
            }

            participants.Add(participant);
        }

        public void RequestInitializationCheck()
        {
            if (State == PawnInitState.GameplayReady
                || State == PawnInitState.Deactivating
                || State == PawnInitState.InitializationFaulted
                || State == PawnInitState.Destroyed)
            {
                return;
            }

            initializationCheckRequested = true;
            InitializationRequested?.Invoke();
        }

        public bool AdvanceInitialization()
        {
            if (!initializationCheckRequested)
            {
                return false;
            }

            bool advanced = false;
            initializationCheckRequested = false;
            while (TryGetNextState(State, out PawnInitState nextState))
            {
                try
                {
                    for (int index = 0; index < participants.Count; index++)
                    {
                        if (!participants[index].CanEnterState(nextState, Context))
                        {
                            return advanced;
                        }
                    }

                    for (int index = 0; index < participants.Count; index++)
                    {
                        participants[index].EnterState(nextState, Context);
                    }

                    State = nextState;
                    advanced = true;
                }
                catch (Exception exception)
                {
                    Fault = exception;
                    State = PawnInitState.InitializationFaulted;
                    InitializationFaulted?.Invoke(exception);
                    return false;
                }
            }

            return advanced;
        }

        public void BeginDeactivation()
        {
            if (State == PawnInitState.Destroyed || State == PawnInitState.Deactivating)
            {
                return;
            }

            State = PawnInitState.Deactivating;
            ShutdownParticipants();
        }

        public void Shutdown()
        {
            if (State == PawnInitState.Destroyed)
            {
                return;
            }

            BeginDeactivation();
            ShutdownParticipants();
            participants.Clear();
            initializationCheckRequested = false;
            InitializationRequested = null;
            InitializationFaulted = null;
            State = PawnInitState.Destroyed;
        }

        private void ShutdownParticipants()
        {
            if (participantsShutdown)
            {
                return;
            }

            participantsShutdown = true;
            for (int index = participants.Count - 1; index >= 0; index--)
            {
                try
                {
                    participants[index].Shutdown();
                }
                catch
                {
                    // Continue reverse cleanup so all bindings are released.
                }
            }
        }

        private static bool TryGetNextState(PawnInitState current, out PawnInitState next)
        {
            switch (current)
            {
                case PawnInitState.Spawned:
                    next = PawnInitState.DataAvailable;
                    return true;
                case PawnInitState.DataAvailable:
                    next = PawnInitState.DataInitialized;
                    return true;
                case PawnInitState.DataInitialized:
                    next = PawnInitState.GameplayReady;
                    return true;
                default:
                    next = default;
                    return false;
            }
        }
    }
}
