using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class LookLayerJob : IAnimationLayerJob
    {
        private LookLayerSettings settings;
        private AnimationUpdateContext context;
        private TransformStreamHandle root;
        private NativeArray<LookJobAtom> pitch;
        private NativeArray<LookJobAtom> yaw;
        private NativeArray<LookJobAtom> roll;
        private Vector2 filteredViewAnglesDegrees;

        public Type SettingsType => typeof(LookLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = layerSettings as LookLayerSettings
                ?? throw new ArgumentException("Look job requires LookLayerSettings.", nameof(layerSettings));
            settings.Validate(jobData.RigComponent.Rig);
            context = jobData.UpdateContext;
            root = jobData.VisualRootHandle;
            try
            {
                pitch = Allocate(jobData, settings.PitchElements, "pitch");
                yaw = Allocate(jobData, settings.YawElements, "yaw");
                roll = Allocate(jobData, settings.RollElements, "roll");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, new LookJob
        {
            Root = root,
            Pitch = pitch,
            Yaw = yaw,
            Roll = roll
        }, 1);

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight)
        {
            filteredViewAnglesDegrees = context.ViewAnglesDegrees;
            if (settings.UseTurnOffset)
            {
                filteredViewAnglesDegrees.x = context.TurnOffsetDegrees;
            }
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            LookJob job = playable.GetJobData<LookJob>();
            job.ViewAnglesDegrees = filteredViewAnglesDegrees;
            job.LeanAngleDegrees = context.LeanAngleDegrees;
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose()
        {
            if (roll.IsCreated) roll.Dispose();
            if (yaw.IsCreated) yaw.Dispose();
            if (pitch.IsCreated) pitch.Dispose();
        }

        private NativeArray<LookJobAtom> Allocate(
            LayerJobData data,
            IReadOnlyList<LookLayerElement> entries,
            string label)
        {
            NativeArray<LookJobAtom> result = new NativeArray<LookJobAtom>(entries.Count, Allocator.Persistent);
            for (int index = 0; index < entries.Count; index++)
            {
                result[index] = new LookJobAtom
                {
                    Handle = RigHandleUtility.Bind(data.Animator, data.RigComponent, entries[index].Element, settings.name + " " + label),
                    AngleLimits = entries[index].AngleLimits
                };
            }
            return result;
        }
    }
}
