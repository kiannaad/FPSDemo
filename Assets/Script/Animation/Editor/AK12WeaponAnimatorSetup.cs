using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CGame.Animation.Editor
{
    internal static class AK12WeaponAnimatorSetup
    {
        private const string ControllerPath = "Assets/Art/Weapon/AK12/AK12WeaponAnimator.controller";
        private const string ReloadClipPath = "Assets/Art/Animation/Weapons/AK12/W_AK12_ReloadEmpty.anim";
        private const string PrefabPath = "Assets/Art/Weapon/AK12/AK12Weapon.prefab";
        private const string DefinitionPath = "Assets/Settings/Gameplay/Weapon/WeaponDefinition/AK12/AK12WeaponDefinition.asset";
        private const string ProfilePath = "Assets/Settings/Gameplay/Weapon/WeaponProfile/AK12/AK12ProceduralBoneProfile.asset";
        private const string CharacterReloadSourcePath = "Assets/Art/Animation/Weapons/General/WeaponAnimations/Generic/C_Rifle_ReloadMixamo.asset";

        [MenuItem("CGame/Weapons/Configure AK12 Reload Animator")]
        private static void Configure()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimationClip reloadClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ReloadClipPath);
            if (controller == null || reloadClip == null)
            {
                throw new System.InvalidOperationException("AK12 reload controller or clip is missing.");
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState reloadState = FindState(stateMachine, "Reload") ?? stateMachine.AddState("Reload");
            reloadState.motion = reloadClip;

            if (FindState(stateMachine, "Idle") == null)
            {
                AnimatorState idleState = stateMachine.AddState("Idle");
                stateMachine.defaultState = idleState;
            }

            ConfigureAk12Profile();
            ConfigureReloadCharacterCurve();
            ConfigurePrefab(controller);
            ConfigureDefinition(controller);
            AssetDatabase.SaveAssets();
            Object definition = AssetDatabase.LoadAssetAtPath<Object>(DefinitionPath);
            SerializedObject serializedDefinition = new SerializedObject(definition);
            GameObject prefab = serializedDefinition.FindProperty("prefab").objectReferenceValue as GameObject;
            Debug.Log($"[ReloadTrace] AK12 asset check: definitionPrefab={AssetDatabase.GetAssetPath(prefab)}, prefabName={(prefab != null ? prefab.name : "<missing>")}");
            if (prefab == null)
            {
                throw new System.InvalidOperationException("AK12 WeaponDefinition prefab reference is missing.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Animator runtimeAnimator = instance.GetComponentInChildren<Animator>(true);
                Debug.Log($"[ReloadTrace] AK12 prefab animator check: animator={(runtimeAnimator != null ? runtimeAnimator.name : "<missing>")}, path={(runtimeAnimator != null ? GetPath(runtimeAnimator.transform, instance.transform) : "<missing>")}, controller={(runtimeAnimator != null && runtimeAnimator.runtimeAnimatorController != null ? runtimeAnimator.runtimeAnimatorController.name : "<null>")}");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            Debug.Log("[ReloadTrace] AK12 reload Animator configured: Reload -> W_AK12_ReloadEmpty");
        }

        private static void ConfigureAk12Profile()
        {
            Object profile = AssetDatabase.LoadAssetAtPath<Object>(ProfilePath);
            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty layers = serializedProfile.FindProperty("layers");
            if (layers == null)
            {
                throw new System.InvalidOperationException("AK12 BoneProfile layers field is missing.");
            }

            for (int index = 0; index < layers.arraySize; index++)
            {
                Object layer = layers.GetArrayElementAtIndex(index).objectReferenceValue;
                if (layer == null)
                {
                    continue;
                }

                string formalName = layer.name
                    .Replace("AK12LayerIntegrationTest", string.Empty)
                    .Replace("AK12", string.Empty);
                if (formalName == "PoseSampler" || formalName == "Ik" || formalName == "AttachHand" || formalName == "View" || formalName == "Ads")
                {
                    layer.name = formalName;
                    EditorUtility.SetDirty(layer);
                    if (formalName == "AttachHand")
                    {
                        EnsureAttachHandCurve(layer);
                    }
                }
            }

            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            Debug.Log("[ReloadTrace] AK12 formal layers configured: PoseSampler, Ik, AttachHand, View, Ads");
        }

        private static void EnsureAttachHandCurve(Object layer)
        {
            SerializedObject serializedLayer = new SerializedObject(layer);
            SerializedProperty blends = serializedLayer.FindProperty("curveBlending");
            if (blends == null)
            {
                throw new System.InvalidOperationException("AttachHand curveBlending field is missing.");
            }

            SerializedProperty target = null;
            for (int index = 0; index < blends.arraySize; index++)
            {
                SerializedProperty candidate = blends.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("curveName").stringValue == "MaskAttachHand")
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null)
            {
                blends.arraySize++;
                target = blends.GetArrayElementAtIndex(blends.arraySize - 1);
            }

            target.FindPropertyRelative("curveName").stringValue = "MaskAttachHand";
            target.FindPropertyRelative("mode").enumValueIndex = 0;
            target.FindPropertyRelative("clampMinimum").floatValue = 0f;
            target.FindPropertyRelative("source").enumValueIndex = 1;
            serializedLayer.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layer);
        }

        private static void ConfigureReloadCharacterCurve()
        {
            Object definition = AssetDatabase.LoadAssetAtPath<Object>(DefinitionPath);
            SerializedObject serializedDefinition = new SerializedObject(definition);
            SerializedProperty characterAnimation = serializedDefinition
                .FindProperty("reloadDefinition")
                .FindPropertyRelative("characterAnimation");
            Object animationAsset = characterAnimation.objectReferenceValue;
            if (animationAsset == null)
            {
                throw new System.InvalidOperationException("AK12 character reload AnimationClipAsset is missing.");
            }

            SerializedObject serializedAnimation = new SerializedObject(animationAsset);
            SerializedProperty animationClip = serializedAnimation.FindProperty("clip");
            if (animationClip != null && animationClip.objectReferenceValue == null)
            {
                Object sourceAnimation = AssetDatabase.LoadAssetAtPath<Object>(CharacterReloadSourcePath);
                if (sourceAnimation != null)
                {
                    SerializedObject serializedSource = new SerializedObject(sourceAnimation);
                    SerializedProperty sourceClip = serializedSource.FindProperty("clip");
                    if (sourceClip != null && sourceClip.objectReferenceValue != null)
                    {
                        animationClip.objectReferenceValue = sourceClip.objectReferenceValue;
                        Debug.Log($"[ReloadTrace] Character reload clip assigned: source={CharacterReloadSourcePath}, clip={sourceClip.objectReferenceValue.name}");
                    }
                }
            }
            SerializedProperty curves = serializedAnimation.FindProperty("namedCurves");
            if (curves == null)
            {
                throw new System.InvalidOperationException("Character AnimationClipAsset namedCurves field is missing.");
            }

            SerializedProperty target = null;
            for (int index = 0; index < curves.arraySize; index++)
            {
                SerializedProperty candidate = curves.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("name").stringValue == "MaskAttachHand")
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null)
            {
                curves.arraySize++;
                target = curves.GetArrayElementAtIndex(curves.arraySize - 1);
            }

            target.FindPropertyRelative("name").stringValue = "MaskAttachHand";
            SerializedProperty curve = target.FindPropertyRelative("curve");
            curve.animationCurveValue = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 0f),
                new Keyframe(0.72f, 0f),
                new Keyframe(0.84f, 0.25f),
                new Keyframe(0.94f, 1f),
                new Keyframe(1f, 1f));
            serializedAnimation.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(animationAsset);
            AnimationClipAsset clipAsset = animationAsset as AnimationClipAsset;
            Debug.Log($"[ReloadTrace] Character reload clip check: clip={(animationClip != null && animationClip.objectReferenceValue != null ? animationClip.objectReferenceValue.name : "<missing>")}");
            if (clipAsset != null && clipAsset.AnimationClip != null)
            {
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clipAsset.AnimationClip);
                int leftHandBindings = 0;
                foreach (EditorCurveBinding binding in bindings)
                {
                    if (binding.path.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || binding.propertyName.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        leftHandBindings++;
                        Debug.Log($"[ReloadTrace] Character reload binding: path={binding.path}, property={binding.propertyName}, type={binding.type.Name}");
                    }
                }

                Debug.Log($"[ReloadTrace] Character reload clip bindings: clip={clipAsset.AnimationClip.name}, total={bindings.Length}, leftHand={leftHandBindings}");
            }
            Debug.Log($"[ReloadTrace] Character reload curve configured: asset={animationAsset.name}, curve=MaskAttachHand, keys=6");
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }

            return null;
        }

        private static void ConfigurePrefab(RuntimeAnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    throw new System.InvalidOperationException("AK12 prefab has no Animator component.");
                }

                Transform animationRoot = FindChild(root.transform, "W_AK12_ReloadEmpty");
                if (animationRoot == null)
                {
                    throw new System.InvalidOperationException("AK12 prefab has no W_AK12_ReloadEmpty animation root.");
                }

                if (animator.transform != animationRoot)
                {
                    Object.DestroyImmediate(animator);
                    animator = animationRoot.gameObject.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static string GetPath(Transform target, Transform root)
        {
            if (target == root)
            {
                return root.name;
            }

            return GetPath(target.parent, root) + "/" + target.name;
        }

        private static void ConfigureDefinition(RuntimeAnimatorController controller)
        {
            Object definition = AssetDatabase.LoadAssetAtPath<Object>(DefinitionPath);
            SerializedObject serializedDefinition = new SerializedObject(definition);
            SerializedProperty property = serializedDefinition.FindProperty("weaponAnimatorController");
            if (property == null)
            {
                throw new System.InvalidOperationException("WeaponDefinition.weaponAnimatorController field is missing.");
            }

            property.objectReferenceValue = controller;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            SerializedProperty profileProperty = serializedDefinition.FindProperty("armedProfile");
            Object profile = profileProperty != null ? profileProperty.objectReferenceValue : null;
            Debug.Log($"[ReloadTrace] AK12 runtime profile reference: path={AssetDatabase.GetAssetPath(profile)}, name={(profile != null ? profile.name : "<missing>")}");
            if (profile != null)
            {
                SerializedObject serializedProfile = new SerializedObject(profile);
                SerializedProperty layers = serializedProfile.FindProperty("layers");
                for (int index = 0; layers != null && index < layers.arraySize; index++)
                {
                    Object layer = layers.GetArrayElementAtIndex(index).objectReferenceValue;
                    Debug.Log($"[ReloadTrace] AK12 runtime profile layer[{index}]={(layer != null ? layer.name : "<missing>")}");
                }
            }
        }
    }
}
