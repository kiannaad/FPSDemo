using System.Collections;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class UpperBodyPlayableRuntimePlayModeTests
    {
        [UnityTest]
        public IEnumerator GameTime_AdvancesActionsAndRestoresNativeOutputAfterDispose()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Character/Kinemation/KinemationVisualCharacter.prefab");
            Assert.NotNull(prefab);
            GameObject visual = Object.Instantiate(prefab);
            Animator animator = visual.GetComponentInChildren<Animator>();
            Assert.NotNull(animator);
            var upperBodyMask = new AvatarMask();
            var controller = new CharacterPlayablesController(animator, upperBodyMask);
            var clip = new AnimationClip
            {
                frameRate = 60f,
            };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 0.1f, 0.01f));
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.IsTrue(asset.TryInitialize(clip));
            asset.BlendInTime = 0.02f;
            asset.BlendOutTime = 0.02f;

            try
            {
                Assert.IsTrue(controller.TryRebuild());
                AnimationPlaybackHandle handle = controller.PlayAnimation(asset, 5001);
                float timeout = Time.realtimeSinceStartup + 2f;
                while (!handle.IsTerminal && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                    controller.Update(Time.deltaTime);
                }

                Assert.AreEqual(AnimationPlaybackState.Completed, handle.State);
                Assert.AreEqual(2, animator.playableGraph.GetOutputCount());
                Assert.AreEqual(0f, animator.playableGraph.GetOutput(0).GetWeight());

                controller.Dispose();
                controller = null;
                Assert.AreEqual(1, animator.playableGraph.GetOutputCount());
                Assert.AreEqual(1f, animator.playableGraph.GetOutput(0).GetWeight());
            }
            finally
            {
                controller?.Dispose();
                Object.Destroy(asset);
                Object.Destroy(clip);
                Object.Destroy(upperBodyMask);
                Object.Destroy(visual);
            }
        }

        [UnityTest]
        public IEnumerator GameTime_PoseRemainsUntilExplicitStop()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Character/Kinemation/KinemationVisualCharacter.prefab");
            Assert.NotNull(prefab);
            GameObject visual = Object.Instantiate(prefab);
            Animator animator = visual.GetComponentInChildren<Animator>();
            var upperBodyMask = new AvatarMask();
            var controller = new CharacterPlayablesController(animator, upperBodyMask);
            var clip = new AnimationClip
            {
                frameRate = 60f,
            };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 0.05f, 0.01f));
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.IsTrue(asset.TryInitialize(clip));
            asset.BlendInTime = 0f;
            asset.BlendOutTime = 0.02f;

            try
            {
                AnimationPlaybackHandle handle = controller.PlayPose(asset, 5002);
                for (int frame = 0; frame < 10; frame++)
                {
                    yield return null;
                    controller.Update(Time.deltaTime);
                }

                Assert.AreEqual(AnimationPlaybackState.Playing, handle.State);
                Assert.IsTrue(controller.Stop(handle));
                float timeout = Time.realtimeSinceStartup + 2f;
                while (!handle.IsTerminal && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                    controller.Update(Time.deltaTime);
                }

                Assert.AreEqual(AnimationPlaybackState.Cancelled, handle.State);
            }
            finally
            {
                controller.Dispose();
                Object.Destroy(asset);
                Object.Destroy(clip);
                Object.Destroy(upperBodyMask);
                Object.Destroy(visual);
            }
        }
    }
}
