using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CGame.Animation.Editor
{
    public sealed class AnimationLayerListWidget
    {
        private readonly BoneProfile profile;
        private readonly SerializedObject serializedProfile;
        private readonly ReorderableList list;

        public AnimationLayerListWidget(BoneProfile profile, SerializedObject serializedProfile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.serializedProfile = serializedProfile ?? throw new ArgumentNullException(nameof(serializedProfile));
            SerializedProperty layers = serializedProfile.FindProperty("layers");
            list = new ReorderableList(serializedProfile, layers, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Animation Layers"),
                drawElementCallback = DrawElement,
                elementHeight = EditorGUIUtility.singleLineHeight + 6f,
                onAddDropdownCallback = ShowAddMenu,
                onRemoveCallback = RemoveSelected,
                onReorderCallbackWithDetails = OnReordered
            };
        }

        public void Draw()
        {
            serializedProfile.Update();
            list.DoLayoutList();
            serializedProfile.ApplyModifiedProperties();
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
            AnimationLayerSettings layer = element.objectReferenceValue as AnimationLayerSettings;
            rect.y += 2f;
            float buttonWidth = 76f;
            Rect labelRect = new Rect(rect.x + 18f, rect.y, rect.width - buttonWidth - 22f, EditorGUIUtility.singleLineHeight);
            Rect editRect = new Rect(labelRect.xMax + 4f, rect.y, buttonWidth, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(labelRect, GetDisplayName(index, layer), EditorStyles.label))
            {
                list.index = index;
            }

            using (new EditorGUI.DisabledScope(layer == null))
            {
                if (GUI.Button(editRect, "Edit Layer"))
                {
                    list.index = index;
                    AnimationLayerEditorWindow.Open(layer);
                }
            }
        }

        private void ShowAddMenu(Rect buttonRect, ReorderableList reorderableList)
        {
            AnimationLayerTypeDropdown.Show(buttonRect, type =>
            {
                BoneProfileLayerAssetService.AddLayer(profile, type);
                serializedProfile.Update();
                list.index = profile.Layers.Count - 1;
            });
        }

        private void RemoveSelected(ReorderableList reorderableList)
        {
            int removeIndex = reorderableList.index;
            if (removeIndex < 0 || removeIndex >= profile.Layers.Count)
            {
                removeIndex = profile.Layers.Count - 1;
            }

            if (removeIndex < 0)
            {
                return;
            }

            BoneProfileLayerAssetService.RemoveLayer(profile, removeIndex);
            serializedProfile.Update();
            reorderableList.index = Mathf.Clamp(removeIndex - 1, -1, profile.Layers.Count - 1);
        }

        private void OnReordered(ReorderableList reorderableList, int oldIndex, int newIndex)
        {
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static string GetDisplayName(int index, AnimationLayerSettings layer)
        {
            string layerType = layer == null ? "Missing Layer" : GetLayerTypeName(layer.GetType().Name);
            return $"{index + 1:D2} · {layerType}";
        }

        private static string GetLayerTypeName(string typeName)
        {
            return typeName switch
            {
                "PoseSamplerLayerSettings" => "Pose Sampler",
                "PoseOffsetLayerSettings" => "Pose Offset",
                "AttachHandLayerSettings" => "Attach Hand",
                "ViewLayerSettings" => "View",
                "AdsLayerSettings" => "ADS",
                "AdditiveLayerSettings" => "Additive",
                "LookLayerSettings" => "Look",
                "TurnLayerSettings" => "Turn",
                "IkMotionLayerSettings" => "IK Motion",
                "IkLayerSettings" => "IK",
                "SwayLayerSettings" => "Sway",
                _ => ObjectNames.NicifyVariableName(typeName.Replace("LayerSettings", string.Empty))
            };
        }
    }
}
