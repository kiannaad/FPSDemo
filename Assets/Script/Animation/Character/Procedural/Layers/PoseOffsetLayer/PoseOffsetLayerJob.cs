using System;
using Unity.Collections;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class PoseOffsetLayerJob : IAnimationLayerJob
    {
        private NativeArray<TransformStreamHandle> handles;
        private NativeArray<PoseOffsetJobData> poses;
        private TransformStreamHandle root;
        private PoseOffsetLayerSettings settings;

        public Type SettingsType => typeof(PoseOffsetLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = RequireSettings(layerSettings);
            root = jobData.CharacterRootHandle;
            Allocate(jobData, settings);
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph)
        {
            return AnimationScriptPlayable.Create(graph, new PoseOffsetJob
            {
                Root = root,
                Handles = handles,
                Poses = poses,
                Weight = 0f
            }, 1);
        }

        public AnimationLayerSettings GetSettings() => settings;

        public void OnPreAnimationUpdate(float deltaTime, float weight) { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            PoseOffsetJob job = playable.GetJobData<PoseOffsetJob>();
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }

        public void Dispose()
        {
            if (poses.IsCreated) poses.Dispose();
            if (handles.IsCreated) handles.Dispose();
        }

        private void Allocate(LayerJobData jobData, PoseOffsetLayerSettings layerSettings)
        {
            int count = layerSettings.PoseOffsets.Count;
            handles = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            poses = new NativeArray<PoseOffsetJobData>(count, Allocator.Persistent);
            for (int index = 0; index < count; index++)
            {
                PoseOffsetEntry entry = layerSettings.PoseOffsets[index];
                handles[index] = RigHandleUtility.Bind(
                    jobData.Animator,
                    jobData.RigComponent,
                    entry.Pose.Element,
                    layerSettings.name + " offset " + index);
                poses[index] = new PoseOffsetJobData(entry.Pose);
            }
        }

        private static PoseOffsetLayerSettings RequireSettings(AnimationLayerSettings value)
        {
            return value as PoseOffsetLayerSettings
                ?? throw new ArgumentException("Pose offset job requires PoseOffsetLayerSettings.", nameof(value));
        }
    }
}
