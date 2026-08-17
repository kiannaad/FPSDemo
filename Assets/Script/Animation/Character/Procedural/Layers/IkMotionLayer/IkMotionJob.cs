using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct IkMotionRuntimeState
    {
        private KTransform playStart;
        private KTransform blendOutStart;
        private float blendOutElapsed;
        private bool isBlendingOut;
        public bool IsPlaying { get; private set; }
        public float Playback { get; private set; }
        public KTransform Result { get; private set; }

        public void Play()
        {
            playStart = Result;
            Playback = 0f;
            blendOutElapsed = 0f;
            isBlendingOut = false;
            IsPlaying = true;
        }

        public void Stop(float blendTime)
        {
            if (!IsPlaying && !isBlendingOut) return;
            if (blendTime <= 0f)
            {
                Result = KTransform.Identity;
                IsPlaying = false;
                isBlendingOut = false;
                return;
            }
            blendOutStart = Result;
            blendOutElapsed = 0f;
            IsPlaying = false;
            isBlendingOut = true;
        }

        public void Advance(float deltaTime, IkMotionLayerSettings settings)
        {
            float safeDelta = Mathf.Max(0f, deltaTime);
            if (isBlendingOut)
            {
                blendOutElapsed += safeDelta;
                Result = KTransform.Lerp(blendOutStart, KTransform.Identity, Mathf.Clamp01(blendOutElapsed / settings.BlendTime));
                if (blendOutElapsed >= settings.BlendTime)
                {
                    Result = KTransform.Identity;
                    isBlendingOut = false;
                }
                return;
            }
            if (!IsPlaying) return;

            Playback = Mathf.Min(Playback + safeDelta * settings.PlayRate, settings.Duration);
            KTransform curvePose = new KTransform(
                Vector3.Scale(settings.TranslationCurves.Evaluate(Playback), settings.TranslationScale),
                Quaternion.Euler(Vector3.Scale(settings.RotationCurves.Evaluate(Playback), settings.RotationScale)));
            float blend = settings.BlendTime <= 0f ? 1f : Mathf.Clamp01(Playback / settings.BlendTime);
            Result = KTransform.Lerp(playStart, curvePose, blend);
            if (Playback >= settings.Duration && settings.AutoBlendOut)
            {
                Stop(settings.BlendTime);
            }
        }
    }

    public struct IkMotionJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public TransformStreamHandle Target;
        public KTransform Motion;
        public float Weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            AnimationLayerJobUtility.ModifyTransform(stream, Root, Target, new KPose
            {
                Pose = Motion,
                Space = TransformSpace.ComponentSpace,
                ModifyMode = TransformModifyMode.Add
            }, Weight);
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
