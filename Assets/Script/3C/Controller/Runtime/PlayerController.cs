using System;
using CGame.Ability;
using UnityEngine;

namespace CGame
{
    public sealed class PlayerController : IWorldController, IController
    {
        private TickFunctionHandle tickHandle;
        private Pawn controlledPawn;
        private IPlayerInputSource inputSource;
        private IEquipmentActionTarget equipmentActionTarget;
        private float controlYaw;
        private float controlPitch;

        private PlayerController(GameSessionId sessionId)
        {
            SessionId = sessionId;
        }

        public GameSessionId SessionId { get; }

        public bool IsActive { get; private set; }

        public PlayerState PlayerState { get; private set; }

        public IInventoryComponent Inventory { get; private set; }

        public IQuickBarComponent QuickBar { get; private set; }

        public IPlayerCameraComponent PlayerCamera { get; private set; }

        public int TickCount { get; private set; }

        public float LastTickDeltaTime { get; private set; }

        public Pawn ControlledPawn => controlledPawn;

        public Quaternion ControlRotation { get; private set; } = Quaternion.identity;

        public static PlayerController Create(
            PlayerControllerCreationContext context,
            IPlayerControllerComponentFactory componentFactory)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (componentFactory == null)
            {
                throw new ArgumentNullException(nameof(componentFactory));
            }

            var controller = new PlayerController(context.SessionId);
            try
            {
                controller.PlayerState = new PlayerState(
                    context.PawnData.ResolveBaseAbilitySets(),
                    context.PawnData);
                controller.Inventory = componentFactory.CreateInventory()
                    ?? throw new InvalidOperationException("Inventory factory returned null.");
                controller.QuickBar = componentFactory.CreateQuickBar(controller.Inventory)
                    ?? throw new InvalidOperationException("QuickBar factory returned null.");
                controller.PlayerCamera = componentFactory.CreatePlayerCamera()
                    ?? throw new InvalidOperationException("PlayerCamera factory returned null.");
                controller.inputSource = context.World.GetCoreService<IPlayerInputSource>();
                controller.tickHandle = context.World.TickScheduler.Register(
                    $"PlayerController[{context.SessionId}]",
                    TickGroup.TG_Controller,
                    controller.Tick,
                    critical: true);
                controller.IsActive = true;
                return controller;
            }
            catch
            {
                controller.Shutdown();
                throw;
            }
        }

        public void Shutdown()
        {
            if (!IsActive && tickHandle == null && PlayerState == null && Inventory == null
                && QuickBar == null && PlayerCamera == null)
            {
                return;
            }

            IsActive = false;
            Unpossess();
            tickHandle?.Dispose();
            tickHandle = null;
            PlayerCamera?.Dispose();
            PlayerCamera = null;
            QuickBar?.Dispose();
            QuickBar = null;
            Inventory?.Dispose();
            Inventory = null;
            PlayerState?.Dispose();
            PlayerState = null;
            inputSource = null;
            equipmentActionTarget = null;
        }

        public void Possess(Pawn pawn)
        {
            if (!IsActive)
            {
                throw new ObjectDisposedException(nameof(PlayerController));
            }

            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            if (ReferenceEquals(controlledPawn, pawn))
            {
                return;
            }

            Unpossess();
            PlayerState.SetAvatar(pawn);
            controlledPawn = pawn;
            pawn.SettingController(this);
        }

        public void Unpossess()
        {
            Pawn oldPawn = controlledPawn;
            controlledPawn = null;
            if (oldPawn == null)
            {
                return;
            }

            oldPawn.ClearingController(this);
            oldPawn.ClearingControlIntent();
            PlayerState?.ClearAvatar(oldPawn);
        }

        public void UpdatingController(float elapseSeconds)
        {
            if (inputSource == null)
            {
                if (PlayerCamera is IPlayerCameraRuntime idleCameraRuntime)
                {
                    idleCameraRuntime.UpdatePresentation(controlledPawn, ControlRotation);
                }
                return;
            }

            Vector2 lookDelta = inputSource.ReadLookDelta(elapseSeconds);
            controlPitch = Mathf.Clamp(controlPitch - lookDelta.y, -89f, 89f);
            controlYaw += lookDelta.x;
            ControlRotation = Quaternion.Euler(controlPitch, controlYaw, 0f);
            controlledPawn?.ApplyingControlRotation(ControlRotation);
            controlledPawn?.SubmitControlIntent(inputSource.ReadControlIntent());
            if (PlayerCamera is IPlayerCameraRuntime cameraRuntime)
            {
                cameraRuntime.UpdatePresentation(controlledPawn, ControlRotation);
            }
            if (inputSource.FirePressed)
            {
                equipmentActionTarget?.Fire();
            }

            if (inputSource.ReloadPressed)
            {
                equipmentActionTarget?.Reload();
            }

            if (inputSource.MeleePressed)
            {
                equipmentActionTarget?.Melee();
            }

            int requestedQuickBarSlot = inputSource.RequestedQuickBarSlot;
            if (requestedQuickBarSlot >= 0)
            {
                QuickBar?.SelectSlot(requestedQuickBarSlot);
            }
        }

        public PawnBindingReceipt BindEquipmentActionTarget(IEquipmentActionTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (equipmentActionTarget != null)
            {
                throw new InvalidOperationException("Controller already has an equipment action target.");
            }

            equipmentActionTarget = target;
            return new PawnBindingReceipt(() =>
            {
                if (ReferenceEquals(equipmentActionTarget, target))
                {
                    equipmentActionTarget = null;
                }
            });
        }

        private void Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return;
            }

            TickCount++;
            LastTickDeltaTime = deltaTime;
            UpdatingController(deltaTime);
        }
    }
}
