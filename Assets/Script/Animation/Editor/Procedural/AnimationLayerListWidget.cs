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
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Ordered Animation Layers"),
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
            float buttonWidth = 48f;
            Rect fieldRect = new Rect(rect.x, rect.y, rect.width - buttonWidth * 3f - 12f, EditorGUIUtility.singleLineHeight);
            Rect editRect = new Rect(fieldRect.xMax + 4f, rect.y, buttonWidth, EditorGUIUtility.singleLineHeight);
            Rect copyRect = new Rect(editRect.xMax + 4f, rect.y, buttonWidth, EditorGUIUtility.singleLineHeight);
            Rect pasteRect = new Rect(copyRect.xMax + 4f, rect.y, buttonWidth, EditorGUIUtility.singleLineHeight);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.ObjectField(fieldRect, layer, typeof(AnimationLayerSettings), false);
            }

            using (new EditorGUI.DisabledScope(layer == null))
            {
                if (GUI.Button(editRect, "Edit")) AnimationLayerEditorWindow.Open(layer);
                if (GUI.Button(copyRect, "Copy")) BoneProfileLayerAssetService.CopyLayer(profile, index);
            }

            using (new EditorGUI.DisabledScope(!BoneProfileLayerAssetService.CanPasteTo(profile, index)))
            {
                if (GUI.Button(pasteRect, "Paste")) BoneProfileLayerAssetService.PasteLayer(profile, index);
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
            if (reorderableList.index < 0 || reorderableList.index >= profile.Layers.Count)
            {
                return;
            }

            BoneProfileLayerAssetService.RemoveLayer(profile, reorderableList.index);
            serializedProfile.Update();
            reorderableList.index = Mathf.Clamp(reorderableList.index - 1, -1, profile.Layers.Count - 1);
        }

        private void OnReordered(ReorderableList reorderableList, int oldIndex, int newIndex)
        {
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
    }
}
