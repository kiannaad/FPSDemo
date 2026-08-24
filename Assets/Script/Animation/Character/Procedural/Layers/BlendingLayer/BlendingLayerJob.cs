using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class BlendingLayerJob : IAnimationLayerJob
    {
        private BlendingLayerSettings settings;
        private TransformStreamHandle root;
        private NativeArray<BlendingJobAtom> elements;

        public Type SettingsType => typeof(BlendingLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = RequireSettings(layerSettings);
            settings.Validate(jobData.RigComponent.Rig);
            root = jobData.VisualRootHandle;
            AllocateSampledPose(jobData);
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph)
        {
            return AnimationScriptPlayable.Create(graph, new BlendingJob
            {
                Root = root,
                Elements = elements,
                BlendPosition = settings.BlendPosition,
                Weight = 0f
            }, 1);
        }

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight) { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            BlendingJob job = playable.GetJobData<BlendingJob>();
            job.Weight = weight;
            job.BlendPosition = settings.BlendPosition;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }

        public void Dispose()
        {
            if (elements.IsCreated) elements.Dispose();
        }

        private void AllocateSampledPose(LayerJobData jobData)
        {
            int count = settings.BlendingElements.Count;
            KTransform[] desiredPoses = new KTransform[count];
            Transform[] transforms = new Transform[count];
            jobData.RigComponent.RestoreInitializedHierarchyPose();
            try
            {
                if (count > 0) settings.DesiredPose.SampleAnimation(jobData.Animator.gameObject, 0f);
                KTransform rootPose = new KTransform(jobData.Animator.transform);
                for (int index = 0; index < count; index++)
                {
                    BlendingLayerElement entry = settings.BlendingElements[index];
                    Transform transform = RigHandleUtility.ResolveTransform(
                        jobData.RigComponent,
                        entry.Element,
                        settings.name + " element " + index);
                    transforms[index] = transform;
                    desiredPoses[index] = rootPose.GetRelativeTransform(new KTransform(transform), false);
                }
            }
            finally
            {
                jobData.RigComponent.RestoreInitializedHierarchyPose();
            }

            try
            {
                elements = new NativeArray<BlendingJobAtom>(count, Allocator.Persistent);
                for (int index = 0; index < count; index++)
                {
                    elements[index] = new BlendingJobAtom
                    {
                        Handle = jobData.Animator.BindStreamTransform(transforms[index]),
                        ActivePose = KTransform.Identity,
                        DesiredComponentPose = desiredPoses[index],
                        ElementWeight = settings.BlendingElements[index].Weight
                    };
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static BlendingLayerSettings RequireSettings(AnimationLayerSettings value)
        {
            return value as BlendingLayerSettings
                ?? throw new ArgumentException("Blending job requires BlendingLayerSettings.", nameof(value));
        }
    }
}
