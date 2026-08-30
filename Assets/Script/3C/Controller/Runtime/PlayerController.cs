using System;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;

namespace CGame
{
    public sealed class PlayerController : Controller
    {
        private readonly PlayerStateDefinition playerStateDefinition;
        private readonly IPlayerInputSource inputSource;
        private readonly IPlayerControllerComponentFactory componentFactory;
        private IEquipmentActionTarget equipmentActionTarget;

        private PlayerController(
            Player player,
            PlayerStateDefinition playerStateDefinition,
            IPlayerInputSource inputSource,
            IPlayerControllerComponentFactory componentFactory)
            : base(player)
        {
            this.playerStateDefinition = playerStateDefinition ?? throw new ArgumentNullException(nameof(playerStateDefinition));
            this.inputSource = inputSource;
            this.componentFactory = componentFactory ?? throw new ArgumentNullException(nameof(componentFactory));
        }

        public bool IsActive { get; private set; }
        public PlayerState PlayerState { get; private set; }
        public PlayerStateDefinition PlayerStateDefinition => playerStateDefinition;
        public IInventoryComponent Inventory { get; private set; }
        public IQuickBarComponent QuickBar { get; private set; }
        public int TickCount { get; private set; }
        public float LastTickDeltaTime { get; private set; }
        public Pawn PossessedPawn => PossessedActor as Pawn;
        public Pawn ControlledPawn => PossessedPawn;
        public Quaternion DesiredRotation { get; private set; } = Quaternion.identity;

        public static PlayerController Create(
            Player player,
            PlayerStateDefinition playerStateDefinition,
            IPlayerInputSource inputSource,
            IPlayerControllerComponentFactory componentFactory)
        {
            return new PlayerController(
                player,
                playerStateDefinition,
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
            SynchronizeDesiredRotation(pawn.Transform != null
                ? pawn.Transform.rotation
                : Quaternion.identity);
            pawn.ApplyingControlRotation(DesiredRotation);
            try
            {
                if (pawn.TryGetComponent(out PawnHeroComponent hero) && hero.HasInputProfile)
                {
                    hero.Bind(inputSource?.InputHandle, PlayerState.AbilitySystem, pawn);
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
            oldPawn.ResetRotationState();
            PlayerState?.ClearAvatar(oldPawn);
        }

        public void UpdatingController(float elapsedSeconds)
        {
            if (inputSource == null || inputSource.InputHandle == null)
            {
                return;
            }

            Vector2 lookDelta = inputSource.ReadLookDelta(elapsedSeconds);
            ControlPitch = Mathf.Clamp(ControlPitch - lookDelta.y, -89f, 89f);
            ControlYaw += lookDelta.x;
            DesiredRotation = Quaternion.Euler(ControlPitch, ControlYaw, 0f);
            PossessedPawn?.ApplyingControlRotation(DesiredRotation);
            PossessedPawn?.ApplyingViewDelta(lookDelta);
            PossessedPawn?.SubmitControlIntent(inputSource.ReadControlIntent());
            int requestedSlot = inputSource.RequestedQuickBarSlot;
            if (requestedSlot >= 0) TryRequestQuickBarSlot(requestedSlot);
            PlayerState?.AbilitySystem.ProcessAbilityInput();
            PlayerState?.AbilitySystem.Tick(elapsedSeconds);
            PossessedPawn?.AdvanceRecoil(elapsedSeconds);
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

public bool TryRequestQuickBarSlot(int slotIndex)
        {
            if (QuickBar == null || slotIndex < 0 || slotIndex >= QuickBar.Slots.Count
                || equipmentActionTarget == null
                || !equipmentActionTarget.CanAcceptDirectSlotSelection(slotIndex))
            {
                return false;
            }

            ItemInstanceHandle handle = QuickBar.Slots[slotIndex];
            if (QuickBar.SelectedSlot == slotIndex && QuickBar.EquippedHandle == handle)
            {
                return true;
            }

            return QuickBar.SelectSlot(slotIndex);
        }

        protected override void OnInitialize()
        {
            PlayerState = new PlayerState(
                playerStateDefinition.ResolveBaseAbilitySets(),
                playerStateDefinition);
            Inventory = componentFactory.CreateInventory()
                ?? throw new InvalidOperationException("Inventory factory returned null.");
            QuickBar = componentFactory.CreateQuickBar(Inventory)
                ?? throw new InvalidOperationException("QuickBar factory returned null.");
            InitialInventorySet initialInventorySet = playerStateDefinition.InitialInventorySet;
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
            ResetDesiredRotation();
        }

        protected override void OnShutdown()
        {
            IsActive = false;
            Unpossess();
            ResetDesiredRotation();
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

        private void ResetDesiredRotation()
        {
            ControlYaw = 0f;
            ControlPitch = 0f;
            DesiredRotation = Quaternion.identity;
        }

        private void SynchronizeDesiredRotation(Quaternion rotation)
        {
            Vector3 euler = rotation.eulerAngles;
            ControlPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -89f, 89f);
            ControlYaw = Mathf.DeltaAngle(0f, euler.y);
            DesiredRotation = Quaternion.Euler(ControlPitch, ControlYaw, 0f);
        }
    }
}
