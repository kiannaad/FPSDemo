using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;

namespace CGame.Animation.Tests
{
    [Category("Network041")]
    public sealed class CharacterAnimationPlayableSeekTests
    {
        [Test]
        public void TryCreate_WithElapsedSeconds_StartsAtClampedPhase()
        {
            var clip = new AnimationClip { frameRate = 60f };
            clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 1f));
            var asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            asset.TryInitialize(clip);
            PlayableGraph graph = PlayableGraph.Create("Network041Seek");
            try
            {
                Assert.That(CharacterAnimationPlayable.TryCreate(
                    graph,
                    asset,
                    1,
                    11,
                    true,
                    0.75f,
                    out CharacterAnimationPlayable playable,
                    out string error), Is.True, error);
                try
                {
                    Assert.That(playable.LocalTime, Is.EqualTo(0.75f).Within(0.0001f));
                    Assert.That(playable.StartTime, Is.EqualTo(0.75f).Within(0.0001f));
                }
                finally
                {
                    playable.Dispose();
                }
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(clip);
            }
        }
    }
}
