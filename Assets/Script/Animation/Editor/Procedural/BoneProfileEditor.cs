using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor
{
    [CustomEditor(typeof(BoneProfile))]
    public sealed class BoneProfileEditor : UnityEditor.Editor
    {
        private AnimationLayerListWidget layerList;
        private SerializedProperty rig;
        private SerializedProperty blendIn;
        private SerializedProperty blendOut;
        private SerializedProperty easeMode;
        private SerializedProperty evaluationTimeout;
        private KRig previousRig;

        private void OnEnable()
        {
            BoneProfile profile = (BoneProfile)target;
            rig = serializedObject.FindProperty("rig");
            blendIn = serializedObject.FindProperty("blendIn");
            blendOut = serializedObject.FindProperty("blendOut");
            easeMode = serializedObject.FindProperty("easeMode");
            evaluationTimeout = serializedObject.FindProperty("evaluationTimeout");
            previousRig = profile.Rig;
            layerList = new AnimationLayerListWidget(profile, serializedObject);
        }

        public override void OnInspectorGUI()
        {
            BoneProfile profile = (BoneProfile)target;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(rig);
            EditorGUILayout.PropertyField(blendIn);
            EditorGUILayout.PropertyField(blendOut);
            EditorGUILayout.PropertyField(easeMode);
            EditorGUILayout.PropertyField(evaluationTimeout);
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed && profile.Rig != previousRig)
            {
                if (profile.Rig != null)
                {
                    BoneProfileLayerAssetService.SynchronizeLayerRigs(profile);
                }

                previousRig = profile.Rig;
            }

            EditorGUILayout.Space();
            layerList.Draw();
            DrawValidation(profile);
        }

        private static void DrawValidation(BoneProfile profile)
        {
            IReadOnlyList<string> ownershipErrors = BoneProfileLayerAssetService.GetOwnershipErrors(profile);
            for (int index = 0; index < ownershipErrors.Count; index++)
            {
                EditorGUILayout.HelpBox(ownershipErrors[index], MessageType.Error);
            }

            try
            {
                profile.Validate(profile.Rig);
                string path = AssetDatabase.GetAssetPath(profile);
                if (path.IndexOf("Knife", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    WeaponBoneProfileValidator.ValidateKnife(profile);
                }
                else if (path.IndexOf("AK12", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    WeaponBoneProfileValidator.ValidateAk12(profile);
                }

                if (ownershipErrors.Count == 0)
                {
                    EditorGUILayout.HelpBox("Bone Profile validation passed.", MessageType.Info);
                }
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }
    }
}
