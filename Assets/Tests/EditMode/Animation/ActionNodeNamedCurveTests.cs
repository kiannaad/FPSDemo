using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;

namespace CGame.Animation.Tests
{
    public sealed class ActionNodeNamedCurveTests
    {
        [Test]
        public void SharedClipAsset_TwoActionInstancesKeepIndependentCurveTime()
        {
            GameObject owner = new GameObject("ActionCurveFixture");
            PlayableGraph graph = PlayableGraph.Create("ActionCurveFixture");
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            try
            {
                var clip = new AnimationClip { name = "ActionCurveClip", frameRate = 30f };
                clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                    AnimationCurve.Linear(0f, 0f, 1f, 0f));
                asset.TryInitialize(clip);
                asset.SetNamedCurve("MaskLeftHandIK", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                var context = new AnimationGraphContext(owner.AddComponent<Animator>(), graph);
                var early = new ActionNode(asset, 10);
                var late = new ActionNode(asset, 10);
                early.Initialize(context);
                late.Initialize(context);
                early.Request(1ul);
                late.Request(2ul);

                early.Update(context, 0.25f);
                late.Update(context, 0.75f);

                Assert.AreEqual(0.25f, early.SampleNamedCurve("MaskLeftHandIK"), 0.01f);
                Assert.AreEqual(0.75f, late.SampleNamedCurve("MaskLeftHandIK"), 0.01f);
                Assert.AreNotEqual(early.NormalizedTime, late.NormalizedTime);
                early.Destroy();
                late.Destroy();
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MissingNamedCurve_ReturnsCallerFallback()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            try
            {
                var clip = new AnimationClip();
                clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                    AnimationCurve.Linear(0f, 0f, 1f, 0f));
                asset.TryInitialize(clip);
                var node = new ActionNode(asset, 1);
                Assert.AreEqual(0.4f, node.SampleNamedCurve("Missing", 0.4f), 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
