using System;

namespace CGame
{
    public sealed class HeroComponent : IPawnInitStateParticipant
    {
        private readonly PlayerController controller;
        private readonly Pawn pawn;
        private PawnBindingReceipt quickBarBinding;
        private PawnBindingReceipt cameraBinding;
        private PawnBindingReceipt equipmentBinding;
        private PawnBindingReceipt equipmentActionBinding;
        private readonly EquipmentManagerComponent equipment;
        private readonly PawnExtensionComponent extension;

        public HeroComponent(
            PlayerController controller,
            Pawn pawn,
            EquipmentManagerComponent equipment,
            PawnExtensionComponent extension)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
        }

        public string Name => "Hero";

        public bool IsBound => quickBarBinding?.IsActive == true
            && cameraBinding?.IsActive == true
            && equipmentBinding?.IsActive == true;

        public bool CanEnterState(PawnInitState nextState, PawnInitContext context)
        {
            if (nextState == PawnInitState.DataInitialized)
            {
                return controller.IsActive
                    && ReferenceEquals(controller.PlayerState.CurrentPawn, pawn)
                    && ReferenceEquals(controller.PlayerState.AbilitySystem.Avatar, pawn);
            }

            return nextState != PawnInitState.GameplayReady || IsBound;
        }

        public void EnterState(PawnInitState nextState, PawnInitContext context)
        {
            if (nextState != PawnInitState.DataInitialized)
            {
                return;
            }

            equipmentBinding = equipment.Bind(controller, pawn, extension);
            try
            {
                equipmentActionBinding = controller.BindEquipmentActionTarget(equipment);
                quickBarBinding = controller.QuickBar.BindPawn(pawn);
                cameraBinding = controller.PlayerCamera.BindPawn(pawn);
            }
            catch
            {
                quickBarBinding?.Dispose();
                quickBarBinding = null;
                equipmentBinding.Dispose();
                equipmentBinding = null;
                equipmentActionBinding?.Dispose();
                equipmentActionBinding = null;
                throw;
            }
        }

        public void Shutdown()
        {
            cameraBinding?.Dispose();
            cameraBinding = null;
            quickBarBinding?.Dispose();
            quickBarBinding = null;
            equipmentBinding?.Dispose();
            equipmentBinding = null;
            equipmentActionBinding?.Dispose();
            equipmentActionBinding = null;
        }
    }
}
