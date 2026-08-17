using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor
{
    public static class BoneProfileLayerAssetService
    {
        private static Type copiedType;
        private static string copiedJson;

        public static AnimationLayerSettings AddLayer(BoneProfile profile, Type settingsType)
        {
            ValidatePersistentProfile(profile);
            if (settingsType == null)
            {
                throw new ArgumentNullException(nameof(settingsType));
            }

            if (!AnimationLayerTypeDropdown.IsSupported(settingsType))
            {
                throw new InvalidOperationException($"Unsupported animation layer type: {settingsType.FullName}.");
            }

            if (profile.Rig == null)
            {
                throw new InvalidOperationException("Assign the Bone Profile Rig before adding layers.");
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Animation Layer");
            AnimationLayerSettings layer = ScriptableObject.CreateInstance(settingsType) as AnimationLayerSettings;
            if (layer == null)
            {
                throw new InvalidOperationException($"{settingsType.FullName} is not an AnimationLayerSettings type.");
            }

            layer.name = ObjectNames.NicifyVariableName(settingsType.Name.Replace("LayerSettings", string.Empty));
            layer.Configure(profile.Rig);
            Undo.RegisterCompleteObjectUndo(profile, "Add Animation Layer");
            Undo.RegisterCreatedObjectUndo(layer, "Add Animation Layer");
            AssetDatabase.AddObjectToAsset(layer, profile);
            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty layers = serializedProfile.FindProperty("layers");
            layers.arraySize++;
            layers.GetArrayElementAtIndex(layers.arraySize - 1).objectReferenceValue = layer;
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(layer);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            return layer;
        }

        public static void RemoveLayer(BoneProfile profile, int index)
        {
            ValidatePersistentProfile(profile);
            AnimationLayerSettings layer = GetLayer(profile, index);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Animation Layer");
            Undo.RegisterCompleteObjectUndo(profile, "Remove Animation Layer");
            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty layers = serializedProfile.FindProperty("layers");
            layers.DeleteArrayElementAtIndex(index);
            if (index < layers.arraySize && layers.GetArrayElementAtIndex(index).objectReferenceValue == null)
            {
                layers.DeleteArrayElementAtIndex(index);
            }

            serializedProfile.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(layer);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
        }

        public static void MoveLayer(BoneProfile profile, int oldIndex, int newIndex)
        {
            ValidatePersistentProfile(profile);
            if (oldIndex < 0 || oldIndex >= profile.Layers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(oldIndex));
            }

            if (newIndex < 0 || newIndex >= profile.Layers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newIndex));
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reorder Animation Layers");
            Undo.RegisterCompleteObjectUndo(profile, "Reorder Animation Layers");
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("layers").MoveArrayElement(oldIndex, newIndex);
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
        }

        public static void CopyLayer(BoneProfile profile, int index)
        {
            AnimationLayerSettings layer = GetLayer(profile, index);
            copiedType = layer.GetType();
            copiedJson = EditorJsonUtility.ToJson(layer);
        }

        public static bool CanPasteTo(BoneProfile profile, int index)
        {
            if (profile == null || index < 0 || index >= profile.Layers.Count)
            {
                return false;
            }

            AnimationLayerSettings layer = profile.Layers[index];
            return layer != null && copiedType == layer.GetType() && !string.IsNullOrEmpty(copiedJson);
        }

        public static void PasteLayer(BoneProfile profile, int index)
        {
            ValidatePersistentProfile(profile);
            AnimationLayerSettings target = GetLayer(profile, index);
            if (!CanPasteTo(profile, index))
            {
                throw new InvalidOperationException("Animation Layer paste requires a copied layer of the same type.");
            }

            string preservedName = target.name;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Paste Animation Layer");
            Undo.RecordObject(target, "Paste Animation Layer");
            EditorJsonUtility.FromJsonOverwrite(copiedJson, target);
            target.name = preservedName;
            target.Configure(profile.Rig);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
        }

        public static void SynchronizeLayerRigs(BoneProfile profile)
        {
            ValidatePersistentProfile(profile);
            if (profile.Rig == null)
            {
                throw new InvalidOperationException("Bone Profile Rig is missing.");
            }

            UnityEngine.Object[] layers = new UnityEngine.Object[profile.Layers.Count];
            for (int index = 0; index < profile.Layers.Count; index++)
            {
                layers[index] = profile.Layers[index];
            }

            if (layers.Length > 0)
            {
                Undo.RecordObjects(layers, "Update Animation Layer Rigs");
            }
            for (int index = 0; index < profile.Layers.Count; index++)
            {
                AnimationLayerSettings layer = profile.Layers[index];
                if (layer == null)
                {
                    throw new InvalidOperationException("Bone Profile contains a missing layer setting.");
                }

                layer.Configure(profile.Rig);
                EditorUtility.SetDirty(layer);
            }

            AssetDatabase.SaveAssets();
        }

        public static IReadOnlyList<string> GetOwnershipErrors(BoneProfile profile)
        {
            List<string> errors = new List<string>();
            if (profile == null)
            {
                errors.Add("Bone Profile is missing.");
                return errors;
            }

            string profilePath = AssetDatabase.GetAssetPath(profile);
            HashSet<AnimationLayerSettings> uniqueLayers = new HashSet<AnimationLayerSettings>();
            for (int index = 0; index < profile.Layers.Count; index++)
            {
                AnimationLayerSettings layer = profile.Layers[index];
                if (layer == null)
                {
                    errors.Add($"Layer {index} is missing.");
                    continue;
                }

                if (!uniqueLayers.Add(layer))
                {
                    errors.Add($"Layer {index} duplicates subasset '{layer.name}'.");
                }

                string layerPath = AssetDatabase.GetAssetPath(layer);
                if (!AssetDatabase.IsSubAsset(layer) || layerPath != profilePath)
                {
                    errors.Add($"Layer {index} '{layer.name}' must be a subasset of this Bone Profile.");
                }
            }

            return errors;
        }

        private static AnimationLayerSettings GetLayer(BoneProfile profile, int index)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (index < 0 || index >= profile.Layers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return profile.Layers[index]
                ?? throw new InvalidOperationException($"Bone Profile layer {index} is missing.");
        }

        private static void ValidatePersistentProfile(BoneProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (!EditorUtility.IsPersistent(profile) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(profile)))
            {
                throw new InvalidOperationException("Bone Profile must be saved as an asset before editing layer subassets.");
            }
        }
    }
}
