using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class ViewLayerJob : IAnimationLayerJob
    {
        private ViewLayerSettings settings;
        private ViewJob job;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private NativeArray<ViewDebugSample> debugSamples;
        private bool hasLoggedDebugSample;
#endif

        public Type SettingsType => typeof(ViewLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = RequireSettings(layerSettings);
            job = new ViewJob
            {
                Root = jobData.VisualRootHandle,
                Weapon = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.IkWeaponBone.Element, settings.name),
                RightHand = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.IkRightHand.Element, settings.name),
                LeftHand = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.IkLeftHand.Element, settings.name),
                WeaponPose = new PoseOffsetJobData(settings.IkWeaponBone),
                RightHandPose = new PoseOffsetJobData(settings.IkRightHand),
                LeftHandPose = new PoseOffsetJobData(settings.IkLeftHand)
            };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugSamples = new NativeArray<ViewDebugSample>(1, Allocator.Persistent);
            job.DebugSamples = debugSamples;
            Debug.Log(
                $"[WeaponIkProbe][ViewInit] Layer={settings.name}; "
                + $"WeaponPose={settings.IkWeaponBone.Pose.Position:F3}; "
                + $"Space={settings.IkWeaponBone.Space}; Mode={settings.IkWeaponBone.ModifyMode}");
#endif
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph)
        {
            return AnimationScriptPlayable.Create(graph, job, 1);
        }

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight) { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hasLoggedDebugSample || !debugSamples.IsCreated || debugSamples[0].Captured == 0)
            {
                return;
            }

            ViewDebugSample sample = debugSamples[0];
            Debug.Log(
                $"[WeaponIkProbe][View] Layer={settings.name}; "
                + $"WeaponBefore={sample.WeaponBefore:F3}; WeaponAfter={sample.WeaponAfter:F3}; "
                + $"Delta={(sample.WeaponAfter - sample.WeaponBefore):F3}; Weight={sample.Weight:F3}");
            hasLoggedDebugSample = true;
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugSamples.IsCreated) debugSamples.Dispose();
            hasLoggedDebugSample = false;
#endif
        }

        private static ViewLayerSettings RequireSettings(AnimationLayerSettings value)
        {
            return value as ViewLayerSettings
                ?? throw new ArgumentException("View job requires ViewLayerSettings.", nameof(value));
        }
    }
}
