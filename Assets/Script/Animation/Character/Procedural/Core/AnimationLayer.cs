using System;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class AnimationLayer : IDisposable
    {
        private readonly PlayableGraph graph;
        private bool isDisposed;

        public AnimationLayer(
            PlayableGraph graph,
            AnimationLayerSettings settings,
            IAnimationLayerJob job,
            AnimationScriptPlayable playable)
        {
            this.graph = graph;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Job = job ?? throw new ArgumentNullException(nameof(job));
            Playable = playable;
            if (!playable.IsValid())
            {
                throw new ArgumentException("Animation layer playable must be valid.", nameof(playable));
            }
        }

        public AnimationLayerSettings Settings { get; }
        public IAnimationLayerJob Job { get; }
        public AnimationScriptPlayable Playable { get; private set; }

        public void PreUpdate(float deltaTime, float weight)
        {
            Job.OnPreAnimationUpdate(deltaTime, weight);
        }

        public void UpdatePlayableJobData(float weight)
        {
            Job.UpdatePlayableJobData(Playable, weight);
        }

        public void PostUpdate()
        {
            Job.OnPostAnimationUpdate();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            if (graph.IsValid() && Playable.IsValid())
            {
                graph.DestroyPlayable(Playable);
            }

            Job.Dispose();
            Playable = AnimationScriptPlayable.Null;
            isDisposed = true;
        }
    }
}
