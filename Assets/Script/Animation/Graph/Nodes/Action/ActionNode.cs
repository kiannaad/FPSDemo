using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class ActionNode : AnimationNodeBase
    {
        private readonly AnimationClipAsset clipAsset;
        private AnimationClipPlayable clipPlayable;
        private AnimationNotifyRuntime notifyRuntime;
        private bool requested;
        private ulong pendingRequestId;
        private float presentationTime;

        public event Action<ActionPresentationEnded> PresentationEnded;

        public ActionNode(AnimationClipAsset clipAsset, int priority)
        {
            this.clipAsset = clipAsset ?? throw new ArgumentNullException(nameof(clipAsset));
            Priority = priority;
        }

        public int Priority { get; }
        public bool IsActive { get; private set; }
        public float Weight { get; internal set; }
        public AnimationClipPlayable ClipPlayable => clipPlayable;
        public ulong RequestId { get; private set; }
        public ulong PendingRequestId => requested ? pendingRequestId : 0ul;
        public float NormalizedTime => clipAsset.AnimationClip == null || clipAsset.AnimationClip.length <= 0f
            ? 0f
            : Mathf.Clamp01(presentationTime / clipAsset.AnimationClip.length);

        public float SampleNamedCurve(string curveName, float fallback = 1f)
        {
            return clipAsset.TryGetNamedCurve(curveName, out AnimationCurve curve)
                ? curve.Evaluate(NormalizedTime)
                : fallback;
        }

        public void Request()
        {
            Request(0ul);
        }

        public void Request(ulong requestId)
        {
            pendingRequestId = requestId;
            requested = true;
        }

        public void Interrupt()
        {
            if (!IsActive)
            {
                return;
            }

            Finish(ActionPresentationEndReason.Interrupted, AnimationNotifyEndReason.Interrupted);
        }

        public bool End(ulong requestId, ActionPresentationEndReason reason)
        {
            if (requested && pendingRequestId == requestId)
            {
                requested = false;
                pendingRequestId = 0ul;
                PresentationEnded?.Invoke(new ActionPresentationEnded(requestId, reason));
                return true;
            }

            if (!IsActive || RequestId != requestId)
            {
                return false;
            }

            Finish(reason, AnimationNotifyEndReason.Interrupted);
            return true;
        }

        public override void Update(AnimationGraphContext context, float deltaTime)
        {
            if (requested)
            {
                requested = false;
                if (IsActive)
                {
                    Finish(ActionPresentationEndReason.Interrupted, AnimationNotifyEndReason.Interrupted);
                }

                IsActive = true;
                RequestId = pendingRequestId;
                pendingRequestId = 0ul;
                clipPlayable.SetTime(0d);
                clipPlayable.SetDone(false);
                presentationTime = 0f;
                context.RecordDebugEvent(nameof(ActionNode), $"Started:{Priority}", 1f);
            }

            if (!IsActive)
            {
                return;
            }

            presentationTime += Mathf.Max(0f, deltaTime) * Mathf.Max(0f, clipAsset.Speed);
            float currentTime = Mathf.Max(presentationTime, Mathf.Max(0f, (float)clipPlayable.GetTime()));
            presentationTime = currentTime;
            notifyRuntime.Tick(currentTime, deltaTime, Weight);
            if (currentTime >= clipAsset.AnimationClip.length)
            {
                Finish(ActionPresentationEndReason.NaturalEnd, AnimationNotifyEndReason.NaturalEnd);
                context.RecordDebugEvent(nameof(ActionNode), $"Ended:{Priority}", currentTime);
            }
        }

        public override AnimationPoseHandle Evaluate(AnimationGraphContext context)
        {
            return new AnimationPoseHandle(clipPlayable, Weight, context.EvaluateFrameId, $"Action:{Priority}");
        }

        public override AnimationNodeDebugSnapshot GetDebugSnapshot()
        {
            return new AnimationNodeDebugSnapshot(nameof(ActionNode), clipPlayable.IsValid(), Weight, 0);
        }

        protected override void OnInitialize(AnimationGraphContext context)
        {
            if (!clipAsset.IsValid)
            {
                throw new InvalidOperationException("ActionNode requires a valid AnimationClipAsset.");
            }

            clipPlayable = AnimationClipPlayable.Create(context.Graph, clipAsset.AnimationClip);
            clipPlayable.SetDuration(clipAsset.AnimationClip.length);
            clipPlayable.SetSpeed(clipAsset.Speed);
            notifyRuntime = new AnimationNotifyRuntime(context.Animator, clipAsset);
        }

        protected override void OnDestroy()
        {
            if (IsActive)
            {
                Finish(ActionPresentationEndReason.OwnerDisposed, AnimationNotifyEndReason.OwnerDisabled);
            }
            else
            {
                notifyRuntime?.EndAll(AnimationNotifyEndReason.OwnerDisabled);
            }
        }

        private void Finish(ActionPresentationEndReason reason, AnimationNotifyEndReason notifyReason)
        {
            ulong endedRequestId = RequestId;
            notifyRuntime?.EndAll(notifyReason);
            IsActive = false;
            Weight = 0f;
            RequestId = 0ul;
            if (endedRequestId > 0ul)
            {
                PresentationEnded?.Invoke(new ActionPresentationEnded(endedRequestId, reason));
            }
        }
    }
}
