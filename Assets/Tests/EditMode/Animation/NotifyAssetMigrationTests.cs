using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Tests
{
    public sealed class NotifyAssetMigrationTests
    {
        [Test]
        public void ExistingClipAssets_KeepValidNotifyConfigurationAfterMigration()
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClipAsset");
            int configuredEvents = 0;

            for (int assetIndex = 0; assetIndex < guids.Length; assetIndex++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[assetIndex]);
                AnimationClipAsset asset = AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                for (int trackIndex = 0; trackIndex < asset.NotifyTracks.Count; trackIndex++)
                {
                    AnimationNotifyTrack track = asset.NotifyTracks[trackIndex];
                    if (track == null)
                    {
                        continue;
                    }

                    for (int eventIndex = 0; eventIndex < track.Events.Count; eventIndex++)
                    {
                        AnimationNotify notify = track.Events[eventIndex]?.Notify;
                        if (notify == null)
                        {
                            continue;
                        }

                        configuredEvents++;
                        AnimationNotifyEvent notifyEvent = track.Events[eventIndex];
                        int maxFrame = Mathf.CeilToInt(asset.AnimationClip.length * asset.AnimationClip.frameRate);
                        Assert.That(notifyEvent.TryValidateForRuntime(maxFrame, out string error), Is.True, $"{path}: {error}");
                    }
                }
            }

            Debug.Log($"Notify asset migration check: clipAssets={guids.Length}, configuredEvents={configuredEvents}");
            Assert.That(guids.Length, Is.GreaterThan(0));
        }
    }
}
