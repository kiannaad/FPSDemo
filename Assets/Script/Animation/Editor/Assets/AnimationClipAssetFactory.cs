using System.IO;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor
{
    public static class AnimationClipAssetFactory
    {
        private const string CreateFromClipMenuPath = "Assets/CGame/Create Animation Clip Asset";
        private const string CreateFromClipCreateMenuPath = "Assets/Create/CGame/Animation/Animation Clip Asset";

        [MenuItem(CreateFromClipMenuPath, false, 20)]
        public static void CreateFromSelectedClip()
        {
            AnimationClip clip = GetSelectedAnimationClip();
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Create Animation Clip Asset", "Select an AnimationClip before creating an Animation Clip Asset.", "OK");
                return;
            }

            CreateFromClip(clip);
        }

        [MenuItem(CreateFromClipCreateMenuPath, false, 20)]
        public static void CreateFromSelectedClipInCreateMenu()
        {
            CreateFromSelectedClip();
        }

        [MenuItem(CreateFromClipMenuPath, true)]
        private static bool CanCreateFromSelectedClip()
        {
            return GetSelectedAnimationClip() != null;
        }

        public static AnimationClipAsset CreateFromClip(AnimationClip clip)
        {
            if (clip == null)
            {
                return null;
            }

            string sourcePath = AssetDatabase.GetAssetPath(clip);
            string directory = string.IsNullOrEmpty(sourcePath)
                ? "Assets"
                : Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "Assets";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{clip.name}ClipAsset.asset");

            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            asset.TryInitialize(clip);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            return asset;
        }

        public static AnimationClipAsset CreateOrUpdateFromClip(AnimationClip clip, string assetPath)
        {
            if (clip == null || string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            AnimationClipAsset existing = AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(assetPath);
            if (existing != null)
            {
                existing.TryInitialize(clip);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory) || !AssetDatabase.IsValidFolder(directory))
            {
                return null;
            }

            AnimationClipAsset created = ScriptableObject.CreateInstance<AnimationClipAsset>();
            created.TryInitialize(clip);
            AssetDatabase.CreateAsset(created, assetPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        public static AnimationClip GetSelectedAnimationClip()
        {
            if (Selection.activeObject is AnimationClip activeClip)
            {
                return activeClip;
            }

            foreach (Object selectedObject in Selection.objects)
            {
                if (selectedObject is AnimationClip clip)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
