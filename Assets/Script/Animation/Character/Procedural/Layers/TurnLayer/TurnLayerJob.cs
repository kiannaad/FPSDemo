using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class TurnLayerJob : IAnimationLayerJob
    {
        private TurnLayerSettings settings;
        private AnimationUpdateContext context;
        private CharacterAnimInstance owner;
        private TransformStreamHandle root;
        private TransformStreamHandle modelRoot;
        private bool offsetPosition;
        private TurnRuntimeState state;
        private TurnRequest pendingRequest;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly int turnLeftStateHash = Animator.StringToHash("TurnInPlace.TurnLeft.Blend Tree");
        private static readonly int turnRightStateHash = Animator.StringToHash("TurnInPlace.TurnRight.Blend Tree");
        private int triggerDiagnosticFrame = -1;
        private string triggerDiagnosticName;
        private bool triggerWasAccepted;
#endif

        public Type SettingsType => typeof(TurnLayerSettings);
        public float TurnOffsetDegrees => -state.AppliedAngle;
        public TurnRequest ConsumeTurnRequest()
        {
            TurnRequest value = pendingRequest;
            pendingRequest = TurnRequest.None;
            return value;
        }

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = layerSettings as TurnLayerSettings
                ?? throw new ArgumentException("Turn job requires TurnLayerSettings.", nameof(layerSettings));
            settings.Validate(jobData.RigComponent.Rig);
            context = jobData.UpdateContext;
            owner = jobData.Owner;
            root = jobData.VisualRootHandle;
            Transform model = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.CharacterRootBone, settings.name);
            Transform hip = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.CharacterHipBone, settings.name);
            modelRoot = jobData.Animator.BindStreamTransform(model);
            offsetPosition = model != hip;
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, new TurnJob
        {
            Root = root,
            ModelRoot = modelRoot,
            TurnAngleDegrees = 0f,
            Weight = 0f,
            OffsetPosition = offsetPosition
        }, 1);

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpdateTriggerDiagnostics();
#endif
            if (!KCurves.IsWeightRelevant(weight))
            {
                context.SetTurnOffsetDegrees(0f);
                return;
            }

            // Movement already owns the physical root facing and the locomotion
            // pose. Turn-in-place must not counter-rotate Skeleton at the same
            // time, otherwise the feet are driven sideways by both systems.
            bool canTurnInPlace = context.CharacterState.IsGrounded
                && !context.CharacterState.IsMoving;
            if (!canTurnInPlace)
            {
                state.Cancel();
                pendingRequest = TurnRequest.None;
                context.SetTurnOffsetDegrees(0f);
                return;
            }

            TurnRequest request = state.Advance(
                context.ViewDeltaDegrees.x,
                deltaTime,
                settings.AngleThreshold,
                settings.TurnSpeed,
                settings.TurnCurve);
            state.ClampForLookYaw(context.ViewAnglesDegrees.x, 90f);
// #if UNITY_EDITOR || DEVELOPMENT_BUILD
//             if (Mathf.Abs(context.ViewDeltaDegrees.x) > 0.01f
//                 && Mathf.Abs(state.AppliedAngle) >= settings.AngleThreshold - 20f)
//             {
//                 Debug.Log(
//                     $"[TurnTriggerProbe] viewDelta={context.ViewDeltaDegrees.x:F3}; "
//                     + $"viewYaw={context.ViewAnglesDegrees.x:F3}; angle={state.AppliedAngle:F3}; "
//                     + $"threshold={settings.AngleThreshold:F3}; weight={weight:F3}; "
//                     + $"isTurning={state.IsTurning}; request={request}",
//                     owner.AnimatorController.Animator);
//             }
// #endif
            if (request != TurnRequest.None)
            {
                pendingRequest = request;
                string triggerName = request == TurnRequest.Left
                    ? settings.AnimatorTurnLeftTrigger
                    : settings.AnimatorTurnRightTrigger;
                context.SetTurnOffsetDegrees(TurnOffsetDegrees * weight);
                bool triggerAccepted = owner.TrySetAnimatorTrigger(triggerName);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                BeginTriggerDiagnostics(triggerName, triggerAccepted);
#endif
                return;
            }

            context.SetTurnOffsetDegrees(TurnOffsetDegrees * weight);
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            TurnJob job = playable.GetJobData<TurnJob>();
            job.TurnAngleDegrees = state.AppliedAngle;
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void BeginTriggerDiagnostics(string triggerName, bool triggerAccepted)
        {
            triggerDiagnosticFrame = 0;
            triggerDiagnosticName = triggerName;
            triggerWasAccepted = triggerAccepted;
            WriteTriggerDiagnostic("request");
        }

        private void UpdateTriggerDiagnostics()
        {
            if (triggerDiagnosticFrame < 0)
            {
                return;
            }

            triggerDiagnosticFrame++;
            if (triggerDiagnosticFrame == 1
                || triggerDiagnosticFrame == 8
                || triggerDiagnosticFrame == 30
                || triggerDiagnosticFrame == 60)
            {
                WriteTriggerDiagnostic("follow-up");
            }

            if (triggerDiagnosticFrame >= 60)
            {
                triggerDiagnosticFrame = -1;
            }
        }

        private void WriteTriggerDiagnostic(string phase)
        {
            CharacterPlayablesController controller = owner.PlayablesController;
            bool hasControllerState = controller.TryGetAnimatorControllerLayerState(
                1,
                out AnimatorStateInfo playableCurrent,
                out AnimatorStateInfo playableNext,
                out bool playableIsTransitioning);
            Animator animator = owner.AnimatorController.Animator;
            bool animatorHasTurnLayer = animator.layerCount > 1;
            float animatorLayerWeight = animatorHasTurnLayer ? animator.GetLayerWeight(1) : -1f;
            AnimatorStateInfo animatorCurrent = animatorHasTurnLayer
                ? animator.GetCurrentAnimatorStateInfo(1)
                : default;
            bool animatorIsTransitioning = animatorHasTurnLayer && animator.IsInTransition(1);
            AnimatorStateInfo animatorNext = animatorIsTransitioning
                ? animator.GetNextAnimatorStateInfo(1)
                : default;
            AnimationLayerMixerPlayable masterMixer = controller.MasterMixer;
            float masterOverlayWeight = masterMixer.IsValid()
                ? masterMixer.GetInputWeight(1)
                : -1f;
            bool controllerInTurnState = IsTurnState(playableCurrent.fullPathHash);
            bool controllerNextIsTurnState = IsTurnState(playableNext.fullPathHash);
            bool animatorInTurnState = IsTurnState(animatorCurrent.fullPathHash);
            bool animatorNextIsTurnState = IsTurnState(animatorNext.fullPathHash);

            // Debug.Log(
            //     $"[TurnTrigger] phase={phase}; frame={triggerDiagnosticFrame}; trigger={triggerDiagnosticName}; "
            //     + $"accepted={triggerWasAccepted}; controllerValid={controller.IsValid()}; "
            //     + $"controllerLayer1Available={hasControllerState}; "
            //     + $"controllerCurrent={playableCurrent.fullPathHash}@{playableCurrent.normalizedTime:F3}; "
            //     + $"controllerInTurnState={controllerInTurnState}; "
            //     + $"controllerTransition={playableIsTransitioning}; "
            //     + $"controllerNext={playableNext.fullPathHash}@{playableNext.normalizedTime:F3}; "
            //     + $"controllerNextIsTurnState={controllerNextIsTurnState}; "
            //     + $"animatorCurrent={animatorCurrent.fullPathHash}@{animatorCurrent.normalizedTime:F3}; "
            //     + $"animatorInTurnState={animatorInTurnState}; "
            //     + $"animatorTransition={animatorIsTransitioning}; "
            //     + $"animatorNext={animatorNext.fullPathHash}@{animatorNext.normalizedTime:F3}; "
            //     + $"animatorNextIsTurnState={animatorNextIsTurnState}; "
            //     + $"turnAngle={state.AppliedAngle:F3}; turnOffset={TurnOffsetDegrees:F3}; "
            //     + $"layerWeight={animatorLayerWeight:F3}; masterOverlayWeight="
            //     + $"{masterOverlayWeight:F3}; slots="
            //     + $"{controller.OverlayActiveSlotCount}/{controller.SlotActiveSlotCount}/{controller.OverrideActiveSlotCount}",
            //     animator);
        }

        private static bool IsTurnState(int stateHash)
        {
            return stateHash == turnLeftStateHash || stateHash == turnRightStateHash;
        }
#endif
    }
}
