using UnityEngine;

namespace CGame
{
    public sealed class AIController : Controller
    {
        private AIControlFrame controlFrame;
        private ICombatIntentSink combatIntentSink;

        public AIControlFrame ControlFrame => controlFrame;

        public void SubmitControlFrame(AIControlFrame frame)
        {
            controlFrame = frame;
        }

        public void SettingCombatIntentSink(ICombatIntentSink sink)
        {
            if (combatIntentSink == sink)
            {
                return;
            }

            combatIntentSink?.SubmitCombatIntent(default);
            combatIntentSink = sink;
        }

        public override void UpdatingController(float elapseSeconds)
        {
            if (ControlledPawn == null)
            {
                return;
            }

            if (controlFrame.AimDirection.sqrMagnitude > 0.000001f)
            {
                SettingControlRotation(Quaternion.LookRotation(controlFrame.AimDirection, Vector3.up));
            }

            base.UpdatingController(elapseSeconds);
            ControlledPawn.SubmitControlIntent(new CharacterControlIntent(
                controlFrame.MovementDirection,
                controlFrame.JumpRequested));
            combatIntentSink?.SubmitCombatIntent(new CharacterCombatIntent(
                controlFrame.AimDirection,
                controlFrame.FireRequested,
                controlFrame.ReloadRequested));

            controlFrame = new AIControlFrame(
                controlFrame.MovementDirection,
                controlFrame.AimDirection,
                false,
                controlFrame.FireRequested,
                false);
        }

        public override void UnpossessingPawn()
        {
            controlFrame = default;
            combatIntentSink?.SubmitCombatIntent(default);
            base.UnpossessingPawn();
        }
    }
}
