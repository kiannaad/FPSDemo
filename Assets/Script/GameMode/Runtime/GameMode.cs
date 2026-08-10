using System;
using System.Collections.Generic;

namespace CGame
{
    public class GameMode
    {
        private readonly World world;
        private WorldControllerRegistration controllerRegistration;
        private TickFunctionHandle tickHandle;
        private PawnAssembly currentPawnAssembly;
        private PawnAssembly candidatePawnAssembly;
        private PlayerStartReservation playerStartReservation;

        public GameMode(
            GameModeCreationContext context,
            ControllerDefinition controllerDefinition,
            PawnData resolvedPawnData)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            world = context.World;
            SessionId = context.SessionId;
            ResolvedPawnData = resolvedPawnData ?? throw new ArgumentNullException(nameof(resolvedPawnData));
            if (controllerDefinition == null)
            {
                throw new ArgumentNullException(nameof(controllerDefinition));
            }

            try
            {
                LocalPlayerController = controllerDefinition.CreateController(
                    new PlayerControllerCreationContext(world, SessionId, ResolvedPawnData));
                if (LocalPlayerController == null)
                {
                    throw new InvalidOperationException("ControllerDefinition returned null.");
                }

                controllerRegistration = world.RegisterController(SessionId, LocalPlayerController);
                tickHandle = world.TickScheduler.Register(
                    $"GameMode[{SessionId}]",
                    TickGroup.TG_GameMode,
                    Tick,
                    critical: true);
                IsActive = true;
            }
            catch
            {
                tickHandle?.Dispose();
                tickHandle = null;
                controllerRegistration?.Dispose();
                controllerRegistration = null;
                LocalPlayerController?.Shutdown();
                LocalPlayerController = null;
                throw;
            }
        }

        public GameSessionId SessionId { get; }

        public PawnData ResolvedPawnData { get; }

        public PlayerController LocalPlayerController { get; private set; }

        public bool IsActive { get; private set; }

        public int TickCount { get; private set; }

        public float LastTickDeltaTime { get; private set; }

        public PawnAssembly CurrentPawnAssembly => currentPawnAssembly;

        public PawnAssembly CandidatePawnAssembly => candidatePawnAssembly;

        public PawnSpawnState SpawnState { get; private set; }

        public string LastSpawnFailure { get; private set; } = string.Empty;

        public void InitializeInventory(InitialInventorySet initialInventorySet)
        {
            IReadOnlyList<ItemInstanceHandle> handles = LocalPlayerController.Inventory.Initialize(initialInventorySet);
            int selectedSlot = initialInventorySet == null ? 0 : initialInventorySet.SelectedSlot;
            LocalPlayerController.QuickBar.Initialize(handles, selectedSlot);
        }

        public bool PrepareLocalPawn(PawnFactory pawnFactory, PlayerStartRegistry playerStartRegistry)
        {
            if (!IsActive)
            {
                throw new ObjectDisposedException(nameof(GameMode));
            }

            if (pawnFactory == null)
            {
                throw new ArgumentNullException(nameof(pawnFactory));
            }

            if (playerStartRegistry == null)
            {
                throw new ArgumentNullException(nameof(playerStartRegistry));
            }

            if (candidatePawnAssembly != null)
            {
                throw new InvalidOperationException("A Pawn candidate is already active for this Controller.");
            }

            SpawnState = PawnSpawnState.Preparing;
            LastSpawnFailure = string.Empty;
            try
            {
                playerStartReservation = playerStartRegistry.ReserveFirstAvailable();
                PlayerStartInfo start = playerStartReservation.Start;
                candidatePawnAssembly = pawnFactory.CreateCandidate(
                    ResolvedPawnData,
                    start.Position,
                    start.Rotation);
                candidatePawnAssembly.AddHero(LocalPlayerController);
                candidatePawnAssembly.Extension.RequestInitializationCheck();
                candidatePawnAssembly.Extension.AdvanceInitialization();
                if (candidatePawnAssembly.Extension.State != PawnInitState.DataAvailable)
                {
                    throw new InvalidOperationException(
                        $"Pawn candidate stopped at {candidatePawnAssembly.Extension.State} instead of DataAvailable.");
                }

                SpawnState = PawnSpawnState.DataAvailable;
                return true;
            }
            catch (Exception exception)
            {
                HandleSpawnFailure(exception);
                return false;
            }
        }

        public bool CommitLocalPawn()
        {
            if (SpawnState != PawnSpawnState.DataAvailable || candidatePawnAssembly == null)
            {
                return false;
            }

            SpawnState = PawnSpawnState.Committing;
            try
            {
                currentPawnAssembly?.Extension.BeginDeactivation();
                LocalPlayerController.Possess(candidatePawnAssembly.Pawn);
                candidatePawnAssembly.Root.SetActive(true);
                candidatePawnAssembly.Extension.RequestInitializationCheck();
                candidatePawnAssembly.Extension.AdvanceInitialization();
                if (candidatePawnAssembly.Extension.State == PawnInitState.InitializationFaulted)
                {
                    throw candidatePawnAssembly.Extension.Fault
                        ?? new InvalidOperationException("Pawn initialization faulted.");
                }

                if (candidatePawnAssembly.Extension.State == PawnInitState.GameplayReady)
                {
                    PromoteCandidate();
                }
                else
                {
                    SpawnState = PawnSpawnState.WaitingForGameplayReady;
                }

                return true;
            }
            catch (Exception exception)
            {
                HandleSpawnFailure(exception);
                return false;
            }
        }

        public bool AdvancePawnInitialization()
        {
            if (SpawnState != PawnSpawnState.WaitingForGameplayReady || candidatePawnAssembly == null)
            {
                return false;
            }

            try
            {
                candidatePawnAssembly.Extension.AdvanceInitialization();
                if (candidatePawnAssembly.Extension.State == PawnInitState.InitializationFaulted)
                {
                    throw candidatePawnAssembly.Extension.Fault
                        ?? new InvalidOperationException("Pawn initialization faulted.");
                }

                if (candidatePawnAssembly.Extension.State == PawnInitState.GameplayReady)
                {
                    PromoteCandidate();
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                HandleSpawnFailure(exception);
                return false;
            }
        }

        public virtual void Shutdown()
        {
            if (!IsActive && controllerRegistration == null && LocalPlayerController == null)
            {
                return;
            }

            IsActive = false;
            playerStartReservation?.Dispose();
            playerStartReservation = null;
            candidatePawnAssembly?.Dispose();
            candidatePawnAssembly = null;
            if (currentPawnAssembly != null)
            {
                LocalPlayerController?.Unpossess();
                currentPawnAssembly.Dispose();
                currentPawnAssembly = null;
            }
            tickHandle?.Dispose();
            tickHandle = null;
            controllerRegistration?.Dispose();
            controllerRegistration = null;
            LocalPlayerController?.Shutdown();
            LocalPlayerController = null;
        }

        private void Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return;
            }

            TickCount++;
            LastTickDeltaTime = deltaTime;
            candidatePawnAssembly?.Equipment.Tick();
            currentPawnAssembly?.Equipment.Tick();
            if (candidatePawnAssembly?.Extension.IsInitializationCheckRequested == true)
            {
                AdvancePawnInitialization();
            }
        }

        private void PromoteCandidate()
        {
            PawnAssembly oldPawnAssembly = currentPawnAssembly;
            currentPawnAssembly = candidatePawnAssembly;
            candidatePawnAssembly = null;
            playerStartReservation?.Dispose();
            playerStartReservation = null;
            oldPawnAssembly?.Dispose();
            SpawnState = PawnSpawnState.GameplayReady;
            world.TryEnterRunning(SessionId);
        }

        private void HandleSpawnFailure(Exception exception)
        {
            Pawn failedPawn = candidatePawnAssembly?.Pawn;
            if (failedPawn != null && ReferenceEquals(LocalPlayerController?.ControlledPawn, failedPawn))
            {
                LocalPlayerController.Unpossess();
            }

            candidatePawnAssembly?.Dispose();
            candidatePawnAssembly = null;
            playerStartReservation?.Dispose();
            playerStartReservation = null;
            SpawnState = PawnSpawnState.Failed;
            LastSpawnFailure = exception?.Message ?? "Pawn spawn failed.";
            if (world.State == WorldState.StartingGame)
            {
                world.TryFailGameStart(SessionId, LastSpawnFailure);
            }
            else if (currentPawnAssembly?.Extension.State == PawnInitState.Deactivating)
            {
                currentPawnAssembly.Dispose();
                currentPawnAssembly = null;
            }
        }
    }
}
