using CGame.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Tests
{
    public class AnimationNodePlayableRuntimeTests
    {
        [Test]
        public void ClipNode_CreatesClipPlayable()
        {
            using (var fixture = new GraphFixture())
            {
                AnimationClip clip = CreateClip();
                var node = new ClipNode(clip, 1.25f);

                node.Initialize(fixture.Context);
                Playable playable = node.Evaluate(fixture.Context);

                Assert.IsTrue(node.IsInitialized);
                Assert.IsTrue(playable.IsValid());
                Assert.AreEqual(typeof(AnimationClipPlayable), playable.GetPlayableType());
                Assert.AreSame(clip, node.Clip);
                Assert.AreEqual(1.25d, playable.GetSpeed());
            }
        }

        [Test]
        public void ClipNode_ConsumesAnimationClipAssetPlaybackSettings()
        {
            using (var fixture = new GraphFixture())
            {
                AnimationClip clip = CreateClip();
                AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
                asset.TryInitialize(clip);
                asset.Speed = 1.5f;
                asset.OverrideNormalizedStartTime = true;
                asset.NormalizedStartTime = 0.25f;
                var node = new ClipNode(asset);

                node.Initialize(fixture.Context);

                Assert.AreSame(asset, node.ClipAsset);
                Assert.AreSame(clip, node.Clip);
                Assert.AreEqual(1.5d, node.ClipPlayable.GetSpeed());
                Assert.AreEqual(clip.length * 0.25d, node.ClipPlayable.GetTime(), 0.001d);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ClipNode_LoopingNodeWrapsTimeAfterClipEnd()
        {
            using (var fixture = new GraphFixture())
            {
                var node = new ClipNode(CreateClip());

                node.Initialize(fixture.Context);
                Assert.IsTrue(double.IsPositiveInfinity(node.ClipPlayable.GetDuration()));
                node.ClipPlayable.SetTime(1.25d);
                node.Update(fixture.Context, 0.016f);

                Assert.AreEqual(0.25d, node.ClipPlayable.GetTime(), 0.001d);
                Assert.IsFalse(node.ClipPlayable.IsDone());
            }
        }

        [Test]
        public void ClipNode_OneShotKeepsTimeAfterClipEnd()
        {
            using (var fixture = new GraphFixture())
            {
                var node = new ClipNode(CreateClip()) { Loop = false };

                node.Initialize(fixture.Context);
                Assert.AreEqual(node.Clip.length, node.ClipPlayable.GetDuration(), 0.001d);
                node.ClipPlayable.SetTime(1.25d);
                node.Update(fixture.Context, 0.016f);

                Assert.AreEqual(1.25d, node.ClipPlayable.GetTime(), 0.001d);
            }
        }

        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip { frameRate = 30f };
            clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }

        private sealed class GraphFixture : System.IDisposable
        {
            private readonly GameObject gameObject;

            public GraphFixture()
            {
                gameObject = new GameObject("AnimationNodeGraphFixture");
                Animator animator = gameObject.AddComponent<Animator>();
                Graph = PlayableGraph.Create("AnimationNodeGraphFixture");
                Context = new AnimationGraphContext(animator, Graph);
            }

            public PlayableGraph Graph { get; }
            public AnimationGraphContext Context { get; }

            public void Dispose()
            {
                if (Graph.IsValid())
                {
                    Graph.Destroy();
                }

                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
