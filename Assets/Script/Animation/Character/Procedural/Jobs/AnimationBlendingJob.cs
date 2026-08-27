using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct AnimationBlendingJob : IAnimationJob
    {
        public NativeArray<TransformStreamPose> Poses;
        public NativeArray<int> CacheCompletionMarker;
        public bool CacheRequested;
        public float BlendDuration;
        public EaseMode EaseMode;
        public float Playback;
        public bool IsBlending;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (CacheRequested && CacheCompletionMarker[0] == 0)
            {
                for (int index = 0; index < Poses.Length; index++)
                {
                    TransformStreamPose pose = Poses[index];
                    pose.LocalPosition = pose.Handle.GetLocalPosition(stream);
                    pose.LocalRotation = pose.Handle.GetLocalRotation(stream);
                    Poses[index] = pose;
                }

                CacheCompletionMarker[0] = 1;
                return;
            }

            if (!IsBlending)
            {
                return;
            }

            Playback += stream.deltaTime;
            float normalizedTime = BlendDuration <= 0f
                ? 1f
                : Mathf.Clamp01(Playback / BlendDuration);
            float weight = EvaluateEase(normalizedTime, EaseMode);
            for (int index = 0; index < Poses.Length; index++)
            {
                TransformStreamPose pose = Poses[index];
                Vector3 activePosition = pose.Handle.GetLocalPosition(stream);
                Quaternion activeRotation = pose.Handle.GetLocalRotation(stream);
                pose.Handle.SetLocalPosition(
                    stream,
                    Vector3.LerpUnclamped(pose.LocalPosition, activePosition, weight));
                pose.Handle.SetLocalRotation(
                    stream,
                    Quaternion.SlerpUnclamped(pose.LocalRotation, activeRotation, weight));
            }

            if (normalizedTime >= 1f)
            {
                IsBlending = false;
            }
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        private static float EvaluateEase(float value, EaseMode mode)
        {
            switch (mode)
            {
                case EaseMode.EaseIn:
                    return value * value;
                case EaseMode.EaseOut:
                    return 1f - (1f - value) * (1f - value);
                case EaseMode.EaseInOut:
                    return value < 0.5f
                        ? 2f * value * value
                        : 1f - Mathf.Pow(-2f * value + 2f, 2f) * 0.5f;
                default:
                    return value;
            }
        }
    }
}
