using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class UpperBodyPlayableRuntimeTests
    {
        [Test]
        public void Rebuild_ReusesNativeControllerSourceAndBuildsOwnedUpperBodyTopology()
        {
            using (var fixture = new PlayablesFixture())
            {
                PlayableGraph graph = fixture.Animator.playableGraph;
                PlayableOutput nativeOutput = graph.GetOutput(0);
                Playable nativeSource = nativeOutput.GetSourcePlayable();

                Assert.IsTrue(fixture.Controller.TryRebuild());

                Assert.AreEqual(2, graph.GetOutputCount());
                Assert.AreEqual(nativeOutput, graph.GetOutput(0));
                Assert.AreEqual(nativeSource, fixture.Controller.NativeControllerSource);
                Assert.AreEqual(nativeSource, fixture.Controller.MasterMixer.GetInput(0));
                Assert.AreEqual(
                    fixture.Controller.OverrideMixer.GetHandle(),
                    fixture.Controller.MasterMixer.GetInput(1).GetHandle());
                Assert.AreEqual(
                    fixture.Controller.OverlayMixer.GetHandle(),
                    fixture.Controller.SlotMixer.GetInput(0).GetHandle());
                Assert.AreEqual(
                    fixture.Controller.SlotMixer.GetHandle(),
                    fixture.Controller.OverrideMixer.GetInput(0).GetHandle());
                Assert.IsFalse(typeof(CharacterPlayablesController)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Any(field => field.FieldType == typeof(AnimatorControllerPlayable)));
                Assert.AreEqual(0f, nativeOutput.GetWeight());
                Assert.AreEqual(1f, graph.GetOutput(1).GetWeight());
            }
        }

        [Test]
        public void PlayAnimation_UsesClipLocalBlendTimesAndAutoCompletesAfterRelease()
        {
            using (var fixture = new PlayablesFixture())
            using (var asset = new ClipAssetFixture(2f))
            {
                asset.Asset.BlendInTime = 0.5f;
                asset.Asset.BlendOutTime = 0.25f;
                asset.Asset.Speed = 2f;
                asset.Asset.OverrideNormalizedStartTime = true;
                asset.Asset.NormalizedStartTime = 0.25f;
                AnimationPlaybackHandle handle = fixture.Controller.PlayAnimation(asset.Asset, 1001);

                Assert.AreEqual(AnimationPlaybackState.BlendingIn, handle.State);
                Assert.IsTrue(fixture.Controller.TrySetPlaybackTime(handle, 0.75));
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(AnimationPlaybackState.BlendingIn, handle.State);

                Assert.IsTrue(fixture.Controller.TrySetPlaybackTime(handle, 1.0));
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(AnimationPlaybackState.Playing, handle.State);

                Assert.IsTrue(fixture.Controller.TrySetPlaybackTime(handle, 2.0));
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(AnimationPlaybackState.BlendingOut, handle.State);

                Assert.IsTrue(fixture.Controller.TrySetPlaybackTime(handle, 2.25));
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(AnimationPlaybackState.Completed, handle.State);
                Assert.IsTrue(handle.IsTerminal);
                Assert.AreEqual(0, fixture.Controller.SlotActiveSlotCount);
            }
        }

        [Test]
        public void PlayPose_DoesNotAutoBlendOutAndManualStopCancelsAfterRelease()
        {
            using (var fixture = new PlayablesFixture())
            using (var asset = new ClipAssetFixture(1f))
            {
                asset.Asset.BlendInTime = 0f;
                asset.Asset.BlendOutTime = 0.2f;
                AnimationPlaybackHandle handle = fixture.Controller.PlayPose(asset.Asset, 1002);

                Assert.IsTrue(fixture.Controller.TrySetPlaybackTime(handle, 2f));
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(AnimationPlaybackState.Playing, handle.State);
                Assert.IsFalse(handle.IsTerminal);

                Assert.IsTrue(fixture.Controller.Stop(handle));
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(AnimationPlaybackState.BlendingOut, handle.State);
                Assert.IsTrue(fixture.Controller.TrySetPlaybackTime(handle, 1.2f));
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(AnimationPlaybackState.Cancelled, handle.State);
                Assert.AreEqual(0, fixture.Controller.OverlayActiveSlotCount);
            }
        }

        [Test]
        public void Channel_UsesThreeTransitionSlotsAndFourthReleasesOldest()
        {
            using (var fixture = new PlayablesFixture())
            using (var first = new ClipAssetFixture(1f))
            using (var second = new ClipAssetFixture(1f))
            using (var third = new ClipAssetFixture(1f))
            using (var fourth = new ClipAssetFixture(1f))
            {
                AnimationPlaybackHandle firstHandle = fixture.Controller.PlayPose(first.Asset, 1);
                AnimationPlaybackHandle secondHandle = fixture.Controller.PlayPose(second.Asset, 2);
                AnimationPlaybackHandle thirdHandle = fixture.Controller.PlayPose(third.Asset, 3);

                Assert.AreEqual(3, fixture.Controller.OverlayActiveSlotCount);
                AnimationPlaybackHandle fourthHandle = fixture.Controller.PlayPose(fourth.Asset, 4);

                Assert.AreEqual(AnimationPlaybackState.Interrupted, firstHandle.State);
                Assert.AreEqual(3, fixture.Controller.OverlayActiveSlotCount);
                Assert.IsFalse(secondHandle.IsTerminal);
                Assert.IsFalse(thirdHandle.IsTerminal);
                Assert.IsFalse(fourthHandle.IsTerminal);
            }
        }

        [Test]
        public void SameClipAndRequestIsIdempotentButDifferentRequestReplays()
        {
            using (var fixture = new PlayablesFixture())
            using (var asset = new ClipAssetFixture(1f))
            {
                AnimationPlaybackHandle first = fixture.Controller.PlayPose(asset.Asset, 40);
                AnimationPlaybackHandle repeated = fixture.Controller.PlayPose(asset.Asset, 40);
                AnimationPlaybackHandle replayed = fixture.Controller.PlayPose(asset.Asset, 41);

                Assert.AreSame(first, repeated);
                Assert.AreNotSame(first, replayed);
                Assert.AreNotEqual(first.PlaybackId, replayed.PlaybackId);
                Assert.AreEqual(2, fixture.Controller.OverlayActiveSlotCount);
            }
        }

        [Test]
        public void CurvesBlendToZeroWhenReplacementOmitsTheCurve()
        {
            using (var fixture = new PlayablesFixture())
            using (var withCurve = new ClipAssetFixture(2f))
            using (var withoutCurve = new ClipAssetFixture(2f))
            {
                withCurve.Asset.BlendInTime = 0f;
                withCurve.Asset.SetNamedCurve(
                    "Grip",
                    AnimationCurve.Constant(0f, 1f, 1f));
                AnimationPlaybackHandle first =
                    fixture.Controller.PlayAnimation(withCurve.Asset, 50);
                fixture.Controller.TrySetPlaybackTime(first, 1f);
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(1f, fixture.Controller.GetCurveValue("Grip"), 0.001f);

                withoutCurve.Asset.BlendInTime = 1f;
                AnimationPlaybackHandle second =
                    fixture.Controller.PlayAnimation(withoutCurve.Asset, 51);
                fixture.Controller.TrySetPlaybackTime(second, 0.5f);
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(0.5f, fixture.Controller.GetCurveValue("Grip"), 0.001f);

                fixture.Controller.TrySetPlaybackTime(second, 1f);
                fixture.Controller.Update(0.016f);
                Assert.AreEqual(0f, fixture.Controller.GetCurveValue("Grip"), 0.001f);
                Assert.AreEqual(AnimationPlaybackState.Interrupted, first.State);
            }
        }

        [Test]
        public void DuplicateCurveNamesFailWithoutCreatingAPlayable()
        {
            using (var fixture = new PlayablesFixture())
            using (var asset = new ClipAssetFixture(1f))
            {
                asset.Asset.SetNamedCurve("Grip", AnimationCurve.Constant(0f, 1f, 1f));
                FieldInfo curvesField = typeof(AnimationClipAsset).GetField(
                    "namedCurves",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(curvesField);
                var curves = (List<AnimationCurveChannel>)curvesField.GetValue(asset.Asset);
                curves.Add(new AnimationCurveChannel(
                    "Grip",
                    AnimationCurve.Constant(0f, 1f, 0f)));

                string expected =
                    $"Animation clip '{asset.Clip.name}' contains duplicate curve 'Grip'.";
                LogAssert.Expect(LogType.Error, expected);
                AnimationPlaybackHandle handle =
                    fixture.Controller.PlayAnimation(asset.Asset, 60);

                Assert.AreEqual(AnimationPlaybackState.Failed, handle.State);
                Assert.AreEqual(0, fixture.Controller.SlotActiveSlotCount);
            }
        }

        [Test]
        public void NotifyDataIsCopiedIntoAClipAssetFreeSnapshot()
        {
            using (var fixture = new PlayablesFixture())
            using (var asset = new ClipAssetFixture(1f))
            {
                AnimationNotifyTrack track = asset.Asset.AddNotifyTrack("Gameplay");
                var notify = new AnimationInstantNotify
                {
                    DisplayName = "Commit",
                    EventTag = "Action.Commit",
                    DispatchPolicy = AnimationNotifyDispatchPolicy.OwnerReceiver,
                };
                notify.SetContextTags(new[] { "Weapon", "Primary" });
                asset.Asset.AddNotifyEvent(0, notify, 12);

                AnimationPlaybackHandle handle =
                    fixture.Controller.PlayAnimation(asset.Asset, 70);
                notify.EventTag = "Mutated";

                Assert.AreEqual(1, handle.NotifySnapshots.Count);
                AnimationNotifySnapshot snapshot = handle.NotifySnapshots[0];
                Assert.AreEqual(track.Name, snapshot.TrackName);
                Assert.AreEqual("Commit", snapshot.DisplayName);
                Assert.AreEqual("Action.Commit", snapshot.EventTag);
                Assert.AreEqual(12, snapshot.StartFrame);
                Assert.AreEqual(2, snapshot.ContextTags.Count);
                Assert.IsFalse(typeof(AnimationNotifySnapshot)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Any(field => typeof(AnimationClipAsset).IsAssignableFrom(field.FieldType)));
            }
        }

        [Test]
        public void OverrideMaskCreatesSynchronizedOverrideAndDisposeRestoresNativeOutput()
        {
            using (var fixture = new PlayablesFixture())
            using (var asset = new ClipAssetFixture(1f))
            {
                asset.Asset.OverrideMask = fixture.UpperBodyMask;
                AnimationPlaybackHandle handle =
                    fixture.Controller.PlayAnimation(asset.Asset, 80);

                Assert.AreEqual(AnimationPlaybackState.BlendingIn, handle.State);
                Assert.AreEqual(1, fixture.Controller.SlotActiveSlotCount);
                Assert.AreEqual(1, fixture.Controller.OverrideActiveSlotCount);

                PlayableGraph graph = fixture.Animator.playableGraph;
                fixture.Controller.Dispose();
                fixture.Controller = null;

                Assert.AreEqual(AnimationPlaybackState.Cancelled, handle.State);
                Assert.AreEqual(1, graph.GetOutputCount());
                Assert.AreEqual(1f, graph.GetOutput(0).GetWeight());
            }
        }

        private sealed class PlayablesFixture : IDisposable
        {
            private readonly GameObject visual;

            public PlayablesFixture()
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Character/Kinemation/KinemationVisualCharacter.prefab");
                Assert.NotNull(prefab);
                visual = UnityEngine.Object.Instantiate(prefab);
                Animator = visual.GetComponentInChildren<Animator>();
                Assert.NotNull(Animator);
                UpperBodyMask = new AvatarMask();
                Controller = new CharacterPlayablesController(Animator, UpperBodyMask);
            }

            public Animator Animator { get; }
            public AvatarMask UpperBodyMask { get; }
            public CharacterPlayablesController Controller { get; set; }

            public void Dispose()
            {
                Controller?.Dispose();
                UnityEngine.Object.DestroyImmediate(UpperBodyMask);
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        private sealed class ClipAssetFixture : IDisposable
        {
            public ClipAssetFixture(float length)
            {
                Clip = new AnimationClip
                {
                    name = $"RuntimeClip-{Guid.NewGuid():N}",
                    frameRate = 60f,
                };
                Clip.SetCurve(
                    string.Empty,
                    typeof(Transform),
                    "localPosition.x",
                    AnimationCurve.Linear(0f, 0f, length, 1f));
                Asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
                Assert.IsTrue(Asset.TryInitialize(Clip));
            }

            public AnimationClip Clip { get; }
            public AnimationClipAsset Asset { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Asset);
                UnityEngine.Object.DestroyImmediate(Clip);
            }
        }
    }
}
