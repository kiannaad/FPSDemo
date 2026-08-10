using System;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;

namespace CGame
{
    public sealed class PlayerController : Controller
    {
        private readonly PawnDefinition pawnDefinition;
        private readonly InitialInventorySet initialInventorySet;
        private readonly IPlayerInputSource inputSource;
        private readonly IPlayerControllerComponentFactory componentFactory;
        private IEquipmentActionTarget equipmentActionTarget;

        private PlayerController(
            Player player,
            PawnDefinition pawnDefinition,
            InitialInventorySet initialInventorySet,
            IPlayerInputSource inputSource,
            IPlayerControllerComponentFactory componentFactory)
            : base(player)
        {
            this.pawnDefinition = pawnDefinition ?? throw new ArgumentNullException(nameof(pawnDefinition));
            this.initialInventorySet = initialInventorySet;
            this.inputSource = inputSource;
            this.componentFactory = componentFactory ?? throw new ArgumentNullException(nameof(componentFactory));
        }

        public bool IsActive { get; private set; }
        public PlayerState PlayerState { get; private set; }
        public IInventoryComponent Inventory { get; private set; }
        public IQuickBarComponent QuickBar { get; private set; }
        public int TickCount { get; private set; }
        public float LastTickDeltaTime { get; private set; }
        public Pawn PossessedPawn => PossessedActor as Pawn;
        public Pawn ControlledPawn => PossessedPawn;
        public Quaternion ControlRotation { get; private set; } = Quaternion.identity;

        public static PlayerController Create(
            Player player,
            PawnDefinition pawnDefinition,
            InitialInventorySet initialInventorySet,
            IPlayerInputSource inputSource,
            IPlayerControllerComponentFactory componentFactory)
        {
            return new PlayerController(
                player,
                pawnDefinition,
                initialInventorySet,
                inputSource,
                componentFactory);
        }

        public void Possess(Pawn pawn)
        {
            if (State != ActorState.Initialized && State != ActorState.Playing)
            {
                throw new InvalidOperationException(
                    $"PlayerController cannot Possess while it is {State}.");
            }

            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            if (ReferenceEquals(PossessedPawn, pawn))
            {
                return;
            }

            Unpossess();
            PlayerState.SetAvatar(pawn);
            AttachPossessedActor(pawn);
            pawn.SettingController(this);
            try
            {
                if (pawn.TryGetComponent(out PawnHeroComponent hero) && hero.HasInputProfile)
                {
                    hero.Bind(inputSource?.InputHandle, PlayerState.AbilitySystem);
                }
            }
            catch
            {
                DetachPossessedActor(pawn);
                pawn.ClearingController(this);
                PlayerState.ClearAvatar(pawn);
                throw;
            }
        }

        public void Unpossess()
        {
            Pawn oldPawn = PossessedPawn;
            if (oldPawn == null)
            {
                return;
            }

            if (oldPawn.TryGetComponent(out PawnHeroComponent hero)) hero.Unbind();
            DetachPossessedActor(oldPawn);
            oldPawn.ClearingController(this);
            oldPawn.ClearingControlIntent();
            PlayerState?.ClearAvatar(oldPawn);
        }

        public void UpdatingController(float elapsedSeconds)
        {
            if (inputSource == null)
            {
                return;
            }

            Vector2 lookDelta = inputSource.ReadLookDelta(elapsedSeconds);
            ControlPitch = Mathf.Clamp(ControlPitch - lookDelta.y, -89f, 89f);
            ControlYaw += lookDelta.x;
            ControlRotation = Quaternion.Euler(ControlPitch, ControlYaw, 0f);
            PossessedPawn?.ApplyingControlRotation(ControlRotation);
            PossessedPawn?.SubmitControlIntent(inputSource.ReadControlIntent());
            int requestedSlot = inputSource.RequestedQuickBarSlot;
            if (requestedSlot >= 0) QuickBar?.SelectSlot(requestedSlot);
            PlayerState?.AbilitySystem.ProcessAbilityInput();
        }

        public PawnBindingReceipt BindEquipmentActionTarget(IEquipmentActionTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            IEquipmentActionTarget previousTarget = equipmentActionTarget;
            equipmentActionTarget = target;
            return new PawnBindingReceipt(() =>
            {
                if (ReferenceEquals(equipmentActionTarget, target))
                {
                    equipmentActionTarget = previousTarget;
                }
            });
        }

        protected override void OnInitialize()
        {
            PlayerState = new PlayerState(
                pawnDefinition.ResolveBaseAbilitySets(),
                pawnDefinition);
            Inventory = componentFactory.CreateInventory()
                ?? throw new InvalidOperationException("Inventory factory returned null.");
            QuickBar = componentFactory.CreateQuickBar(Inventory)
                ?? throw new InvalidOperationException("QuickBar factory returned null.");
            if (initialInventorySet != null)
            {
                IReadOnlyList<ItemInstanceHandle> handles = Inventory.Initialize(initialInventorySet);
                QuickBar.Initialize(handles, initialInventorySet.SelectedSlot);
            }

            AddTickTask("PlayerController", TickGroup.TG_Gameplay, Tick);
        }

        protected override void OnBeginPlay()
        {
            IsActive = true;
        }

        protected override void OnEndPlay()
        {
            IsActive = false;
            Unpossess();
        }

        protected override void OnShutdown()
        {
            IsActive = false;
            Unpossess();
            equipmentActionTarget = null;
            QuickBar?.Dispose();
            QuickBar = null;
            Inventory?.Dispose();
            Inventory = null;
            PlayerState?.Dispose();
            PlayerState = null;
        }

        protected override void OnPossessedActorUnregistered(Actor actor)
        {
            if (actor is Pawn pawn)
            {
                pawn.ClearingControlIntent();
                pawn.ClearingController(this);
                PlayerState?.ClearAvatar(pawn);
            }
        }

        private void Tick(float deltaTime)
        {
            TickCount++;
            LastTickDeltaTime = deltaTime;
            UpdatingController(deltaTime);
        }
    }
}
