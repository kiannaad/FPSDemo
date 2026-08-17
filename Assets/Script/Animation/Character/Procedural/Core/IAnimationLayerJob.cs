using System;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public interface IAnimationLayerJob : IDisposable
    {
        Type SettingsType { get; }
        void Initialize(LayerJobData jobData, AnimationLayerSettings settings);
        AnimationScriptPlayable CreatePlayable(PlayableGraph graph);
        AnimationLayerSettings GetSettings();
        void OnPreAnimationUpdate(float deltaTime, float weight);
        void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight);
        void OnPostAnimationUpdate();
    }
}
