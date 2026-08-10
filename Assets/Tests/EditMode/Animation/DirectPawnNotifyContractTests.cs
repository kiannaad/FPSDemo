using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditorInternal;
using UnityEngine;

namespace CGame.Animation.Tests
{
    public sealed class DirectPawnNotifyContractTests
    {
        private static readonly string TempSerializedPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp/TempNotifyContract.asset"));

        [SetUp]
        public void SetUp()
        {
            RecordingInstantNotify.Reset();
            RecordingDurationNotify.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(TempSerializedPath))
            {
                File.Delete(TempSerializedPath);
            }
        }

        [Test]
        public void NotifyCallbacks_ReceiveTheExactPawnDirectly()
        {
            Pawn pawn = new Pawn();
            RecordingInstantNotify instant = new RecordingInstantNotify();
            RecordingDurationNotify duration = new RecordingDurationNotify();

            instant.OnNotify(pawn);
            duration.OnBegin(pawn);
            duration.OnTick(pawn);
            duration.OnEnd(pawn, AnimationNotifyEndReason.StateStopped);

            Assert.That(RecordingInstantNotify.LastPawn, Is.SameAs(pawn));
            Assert.That(RecordingDurationNotify.Trace, Is.EqualTo("begin,tick,end:StateStopped"));
            Assert.That(RecordingDurationNotify.LastPawn, Is.SameAs(pawn));
        }

        [Test]
        public void NotifyDefinition_ContainsConfigurationButNoPlaybackState()
        {
            string[] fieldNames = typeof(AnimationNotify)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(fieldNames, Is.EqualTo(new[] { "displayName", "fadeOutPolicy" }));
            Assert.That(typeof(AnimationInstantNotify).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), Is.Empty);
            Assert.That(typeof(AnimationDurationNotify).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), Is.Empty);
        }

        [Test]
        public void NotifyConfiguration_SavesAndReloadsWithSerializeReference()
        {
            AnimationClipAsset asset = CreateClipAsset();
            AnimationNotifyTrack track = asset.AddNotifyTrack("Gameplay");
            AnimationDurationNotify notify = new AnimationDurationNotify
            {
                DisplayName = "ReloadWindow",
                FadeOutPolicy = AnimationNotifyFadeOutPolicy.FinishActiveDuration,
            };
            track.AddEvent(notify, 3, 4).MinTriggerWeight = 0.25f;

            InternalEditorUtility.SaveToSerializedFileAndForget(
                new UnityEngine.Object[] { asset, asset.AnimationClip },
                TempSerializedPath,
                true);

            UnityEngine.Object[] reloadedObjects = InternalEditorUtility.LoadSerializedFileAndForget(TempSerializedPath);
            try
            {
                AnimationClipAsset reloaded = reloadedObjects.OfType<AnimationClipAsset>().Single();
                AnimationNotifyEvent reloadedEvent = reloaded.NotifyTracks[0].Events[0];
                AnimationDurationNotify reloadedNotify = reloadedEvent.Notify as AnimationDurationNotify;

                Assert.That(reloadedNotify, Is.Not.Null);
                Assert.That(reloadedNotify.DisplayName, Is.EqualTo("ReloadWindow"));
                Assert.That(reloadedNotify.FadeOutPolicy, Is.EqualTo(AnimationNotifyFadeOutPolicy.FinishActiveDuration));
                Assert.That(reloadedEvent.StartFrame, Is.EqualTo(3));
                Assert.That(reloadedEvent.DurationFrames, Is.EqualTo(4));
                Assert.That(reloadedEvent.MinTriggerWeight, Is.EqualTo(0.25f));
                Assert.That(RecordingDurationNotify.Trace, Is.Empty, "Editor serialization must not execute runtime callbacks.");
            }
            finally
            {
                for (int objectIndex = 0; objectIndex < reloadedObjects.Length; objectIndex++)
                {
                    UnityEngine.Object.DestroyImmediate(reloadedObjects[objectIndex]);
                }
            }
        }

        [Test]
        public void RuntimeValidation_EnforcesInstantAndDurationFrameRules()
        {
            AnimationNotifyEvent validInstant = new AnimationNotifyEvent { Notify = new AnimationInstantNotify(), DurationFrames = 0 };
            AnimationNotifyEvent invalidInstant = new AnimationNotifyEvent { Notify = new AnimationInstantNotify(), DurationFrames = 1 };
            AnimationNotifyEvent validDuration = new AnimationNotifyEvent { Notify = new AnimationDurationNotify(), DurationFrames = 1 };
            AnimationNotifyEvent invalidDuration = new AnimationNotifyEvent { Notify = new AnimationDurationNotify(), DurationFrames = 0 };

            Assert.That(validInstant.TryValidateForRuntime(10, out _), Is.True);
            Assert.That(invalidInstant.TryValidateForRuntime(10, out _), Is.False);
            Assert.That(validDuration.TryValidateForRuntime(10, out _), Is.True);
            Assert.That(invalidDuration.TryValidateForRuntime(10, out _), Is.False);
        }

        [Test]
        public void InvalidNotifyConfiguration_DoesNotMakeClipPlaybackValidationFail()
        {
            AnimationClipAsset asset = CreateClipAsset();
            AnimationNotifyEvent invalid = asset.AddNotifyTrack().AddEvent(new AnimationInstantNotify(), 0, 1);

            Assert.That(invalid.TryValidateForRuntime(30, out _), Is.False);
            Assert.That(asset.TryValidateForPlayback(out string error), Is.True, error);
        }

        private static AnimationClipAsset CreateClipAsset()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            AnimationClip clip = new AnimationClip
            {
                frameRate = 30f,
                name = "NotifyContractClip",
            };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            Assert.That(asset.TryInitialize(clip), Is.True);
            return asset;
        }

        [Serializable]
        private sealed class RecordingInstantNotify : AnimationInstantNotify
        {
            public static Pawn LastPawn { get; private set; }

            public static void Reset()
            {
                LastPawn = null;
            }

            public override void OnNotify(Pawn pawn)
            {
                LastPawn = pawn;
            }
        }

        [Serializable]
        private sealed class RecordingDurationNotify : AnimationDurationNotify
        {
            public static Pawn LastPawn { get; private set; }
            public static string Trace { get; private set; } = string.Empty;

            public static void Reset()
            {
                LastPawn = null;
                Trace = string.Empty;
            }

            public override void OnBegin(Pawn pawn)
            {
                LastPawn = pawn;
                Trace = "begin";
            }

            public override void OnTick(Pawn pawn)
            {
                LastPawn = pawn;
                Trace += ",tick";
            }

            public override void OnEnd(Pawn pawn, AnimationNotifyEndReason reason)
            {
                LastPawn = pawn;
                Trace += $",end:{reason}";
            }
        }
    }
}
