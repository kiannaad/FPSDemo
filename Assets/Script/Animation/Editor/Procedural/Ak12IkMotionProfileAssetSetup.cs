using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor
{
    public static class Ak12IkMotionProfileAssetSetup
    {
        private const string ProfilePath =
            "Assets/Art/Weapon/Profile/AK12/AK12ProceduralBoneProfile.asset";

        [MenuItem("CGame/Animation/Procedural/Normalize AK12 IK Motion Profile")]
        public static void Normalize()
        {
            BoneProfile profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException("AK12 procedural BoneProfile is missing.");
            }

            bool includesAds = profile.Layers.Any(layer => layer is AdsLayerSettings);
            Type[] expectedTypes = includesAds
                ? new[]
                {
                    typeof(PoseSamplerLayerSettings), typeof(IkMotionLayerSettings),
                    typeof(AttachHandLayerSettings), typeof(ViewLayerSettings), typeof(AdsLayerSettings),
                    typeof(AdditiveLayerSettings), typeof(LookLayerSettings), typeof(TurnLayerSettings),
                    typeof(IkLayerSettings)
                }
                : new[]
                {
                    typeof(PoseSamplerLayerSettings), typeof(IkMotionLayerSettings),
                    typeof(AttachHandLayerSettings), typeof(ViewLayerSettings), typeof(AdditiveLayerSettings),
                    typeof(LookLayerSettings), typeof(TurnLayerSettings), typeof(IkLayerSettings)
                };

            var orderedLayers = new List<AnimationLayerSettings>(expectedTypes.Length);
            foreach (Type expectedType in expectedTypes)
            {
                AnimationLayerSettings[] matches = profile.Layers
                    .Where(layer => layer != null && layer.GetType() == expectedType)
                    .ToArray();
                if (matches.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"AK12 profile requires exactly one {expectedType.Name}, but found {matches.Length}.");
                }

                orderedLayers.Add(matches[0]);
            }

            if (profile.Layers.Count != orderedLayers.Count)
            {
                throw new InvalidOperationException(
                    "AK12 profile contains an unsupported layer; normalize its layer set before reordering it.");
            }

            profile.Configure(profile.Rig, orderedLayers);
            profile.Validate(profile.Rig);
            WeaponBoneProfileValidator.ValidateAk12(profile);
            EditorUtility.SetDirty(profile);

            NormalizeKnife();
            AssetDatabase.SaveAssets();
            Debug.Log($"Normalized {profile.name}: {string.Join(" -> ", orderedLayers.Select(layer => layer.GetType().Name))}", profile);
        }

        private static void NormalizeKnife()
        {
            const string knifeProfilePath = "Assets/Art/Weapon/Profile/Knife/KnifeBoneProfile.asset";
            BoneProfile profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(knifeProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException("Knife procedural BoneProfile is missing.");
            }

            Type[] expectedTypes =
            {
                typeof(PoseSamplerLayerSettings),
                typeof(IkMotionLayerSettings),
                typeof(IkLayerSettings)
            };
            var orderedLayers = new List<AnimationLayerSettings>(expectedTypes.Length);
            foreach (Type expectedType in expectedTypes)
            {
                AnimationLayerSettings[] matches = profile.Layers
                    .Where(layer => layer != null && layer.GetType() == expectedType)
                    .ToArray();
                if (matches.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Knife profile requires exactly one {expectedType.Name}, but found {matches.Length}.");
                }

                orderedLayers.Add(matches[0]);
            }

            if (profile.Layers.Count != orderedLayers.Count)
            {
                throw new InvalidOperationException(
                    "Knife profile contains an unsupported layer; normalize its layer set before reordering it.");
            }

            profile.Configure(profile.Rig, orderedLayers);
            profile.Validate(profile.Rig);
            WeaponBoneProfileValidator.ValidateKnife(profile);
            EditorUtility.SetDirty(profile);
            Debug.Log($"Normalized {profile.name}: {string.Join(" -> ", orderedLayers.Select(layer => layer.GetType().Name))}", profile);
        }
    }
}
