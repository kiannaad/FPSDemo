using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CGame.Network;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CGame.Editor
{
    public static class EnemyPresentationAssetMigration
    {
        private const string TargetDirectory = "Assets/Art/Characters/Enemies/TPSBundle/Prefabs";
        private const string ArtDirectory = "Assets/Art/Characters/Enemies/TPSBundle/Art";
        private const string AnimatorDirectory = "Assets/Art/Characters/Enemies/TPSBundle/Animator";
        private const string SettingsDirectory = "Assets/Settings/Gameplay/Enemy";
        private static readonly (string Source, string Target)[] Prefabs =
        {
            ("Assets/TPS Bundle/EnemyAI/Examples/Prefabs/enemy_pistol.prefab", "NetworkEnemyPistol.prefab"),
            ("Assets/TPS Bundle/EnemyAI/Examples/Prefabs/enemy_rifle.prefab", "NetworkEnemyRifle.prefab"),
            ("Assets/TPS Bundle/EnemyAI/Examples/Prefabs/enemy_ak.prefab", "NetworkEnemyAk.prefab")
        };

        [MenuItem("CGame/Enemy/Migrate TPS Bundle Presentation Prefabs")]
        public static void MigratePrefabs()
        {
            EnsureFolder(TargetDirectory);
            foreach ((string source, string targetName) in Prefabs)
            {
                string target = TargetDirectory + "/" + targetName;
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(target) && !AssetDatabase.CopyAsset(source, target))
                    throw new InvalidOperationException($"Unable to copy enemy presentation prefab: {source}.");

                Dictionary<string, string> copiedPaths = CopyArtDependencies(source);

                GameObject root = PrefabUtility.LoadPrefabContents(target);
                try
                {
                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    for (int index = transforms.Length - 1; index >= 0; index--)
                    {
                        GameObject candidate = transforms[index].gameObject;
                        if (PrefabUtility.IsOutermostPrefabInstanceRoot(candidate))
                            PrefabUtility.UnpackPrefabInstance(candidate, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                    foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true))
                        UnityEngine.Object.DestroyImmediate(agent);
                    foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                        UnityEngine.Object.DestroyImmediate(behaviour);
                    EnemyPresentation presentation = root.GetComponent<EnemyPresentation>();
                    if (presentation == null) presentation = root.AddComponent<EnemyPresentation>();
                    Animator animator = root.GetComponentInChildren<Animator>(true);
                    if (animator == null) throw new InvalidOperationException($"Enemy presentation prefab has no Animator: {source}.");
                    animator.applyRootMotion = false;
                    animator.runtimeAnimatorController = CreateAnimatorController(targetName);
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.avatar = FindCopiedAsset<Avatar>(animator.avatar, copiedPaths);
                    RebindRendererAssets(root, copiedPaths);
                    RebindMaterialTextures(copiedPaths);
                    presentation.Configure(animator);
                    PrefabUtility.SaveAsPrefabAsset(root, target);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            CreateCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("CGame/Enemy/Preview TPS Bundle Presentation Prefabs")]
        public static void PreviewPrefabs()
        {
            EnemyPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>("Assets/Settings/Gameplay/Enemy/EnemyPresentationCatalog.asset");
            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.transform.position = new Vector3(0f, 1.4f, -10f);
                preview.camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                preview.camera.fieldOfView = 42f;
                preview.camera.farClipPlane = 50f;
                for (int index = 0; index < catalog.Archetypes.Count; index++)
                {
                    GameObject instance = preview.InstantiatePrefabInScene(catalog.Archetypes[index].PresentationPrefab);
                    instance.transform.position = new Vector3((index - 1) * 3.25f, 0f, 0f);
                }
                preview.BeginPreview(new Rect(0, 0, 1280, 720), GUIStyle.none);
                preview.camera.Render();
                var output = preview.EndPreview() as RenderTexture;
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = output;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                string runDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".harness", "runs", "TPSBundleEnemyPresentationClosure-050-preview"));
                Directory.CreateDirectory(runDirectory);
                File.WriteAllBytes(Path.Combine(runDirectory, "after-preview-1280x720.png"), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }

        private static Dictionary<string, string> CopyArtDependencies(string prefabPath)
        {
            var copied = new Dictionary<string, string>();
            foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
            {
                if (!dependency.StartsWith("Assets/TPS Bundle/EnemyAI/", StringComparison.Ordinal) || dependency.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                string relative = dependency.Substring("Assets/TPS Bundle/EnemyAI/".Length);
                string destination = ArtDirectory + "/" + relative;
                EnsureFolder(Path.GetDirectoryName(destination)?.Replace('\\', '/'));
                if (!AssetDatabase.LoadMainAssetAtPath(destination) && !AssetDatabase.CopyAsset(dependency, destination))
                    throw new InvalidOperationException($"Unable to copy enemy art dependency: {dependency}.");
                copied[dependency] = destination;
            }
            return copied;
        }

        private static RuntimeAnimatorController CreateAnimatorController(string prefabName)
        {
            EnsureFolder(AnimatorDirectory);
            string path = AnimatorDirectory + "/" + prefabName.Replace(".prefab", ".controller");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MoveDirection", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Fire", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureStateMachine(controller, prefabName);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureStateMachine(AnimatorController controller, string prefabName)
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            // Earlier migration versions saved parameter-only controllers.  Some
            // of those assets retain an orphan default-state reference, so only
            // treat the controller as initialized when it has real states.
            if (stateMachine.defaultState != null && stateMachine.states.Length > 0) return;

            AnimationClip idle = LoadClip("Animations/Move/idle.anim");
            AnimationClip move = LoadClip("Animations/Move/Strafe/strafe_fwd.anim");
            AnimationClip hit = LoadClip("Animations/Shoot/hit.anim");
            string weapon = prefabName.Contains("Pistol", StringComparison.Ordinal) ? "pistol" : "rifle";
            AnimationClip fire = LoadClip($"Animations/Shoot/{weapon}_shot.anim");

            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState moveState = stateMachine.AddState("Move");
            moveState.motion = move;
            AnimatorState fireState = stateMachine.AddState("Fire");
            fireState.motion = fire;
            AnimatorState hitState = stateMachine.AddState("Hit");
            hitState.motion = hit;
            stateMachine.defaultState = idleState;

            AnimatorStateTransition beginMove = idleState.AddTransition(moveState);
            beginMove.hasExitTime = false;
            beginMove.duration = 0.08f;
            beginMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
            AnimatorStateTransition endMove = moveState.AddTransition(idleState);
            endMove.hasExitTime = false;
            endMove.duration = 0.08f;
            endMove.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            AnimatorStateTransition fireTransition = stateMachine.AddAnyStateTransition(fireState);
            fireTransition.hasExitTime = false;
            fireTransition.duration = 0.04f;
            fireTransition.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
            AnimatorStateTransition fireExit = fireState.AddTransition(idleState);
            fireExit.hasExitTime = true;
            fireExit.exitTime = 0.95f;
            fireExit.duration = 0.04f;
            AnimatorStateTransition hitTransition = stateMachine.AddAnyStateTransition(hitState);
            hitTransition.hasExitTime = false;
            hitTransition.duration = 0.04f;
            hitTransition.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            AnimatorStateTransition hitExit = hitState.AddTransition(idleState);
            hitExit.hasExitTime = true;
            hitExit.exitTime = 0.95f;
            hitExit.duration = 0.04f;
        }

        private static AnimationClip LoadClip(string relativePath)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtDirectory + "/" + relativePath);
            if (clip == null) throw new InvalidOperationException($"Enemy animation clip is missing: {relativePath}.");
            return clip;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
                if (parameter.name == name && parameter.type == type) return;
            controller.AddParameter(name, type);
        }

        private static void RebindRendererAssets(GameObject root, IReadOnlyDictionary<string, string> copiedPaths)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skinned) skinned.sharedMesh = FindCopiedAsset<Mesh>(skinned.sharedMesh, copiedPaths);
                else if (renderer is MeshRenderer meshRenderer && meshRenderer.TryGetComponent(out MeshFilter filter)) filter.sharedMesh = FindCopiedAsset<Mesh>(filter.sharedMesh, copiedPaths);
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++) materials[index] = FindCopiedAsset<Material>(materials[index], copiedPaths);
                renderer.sharedMaterials = materials;
            }
        }

        private static void RebindMaterialTextures(IReadOnlyDictionary<string, string> copiedPaths)
        {
            Shader compatibleShader = Shader.Find("Universal Render Pipeline/Lit");
            if (compatibleShader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable for TPS Bundle enemy presentation materials.");

            foreach (string destination in copiedPaths.Values)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(destination);
                if (material == null) continue;
                Texture mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                material.shader = compatibleShader;
                if (mainTexture != null) material.SetTexture("_BaseMap", FindCopiedAsset<Texture>(mainTexture, copiedPaths));
                foreach (string property in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(property);
                    Texture replacement = FindCopiedAsset<Texture>(texture, copiedPaths);
                    if (replacement != texture) material.SetTexture(property, replacement);
                }
                EditorUtility.SetDirty(material);
            }
        }

        private static T FindCopiedAsset<T>(T source, IReadOnlyDictionary<string, string> copiedPaths) where T : UnityEngine.Object
        {
            if (source == null) return null;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (!copiedPaths.TryGetValue(sourcePath, out string destination)) return source;
            return AssetDatabase.LoadAllAssetsAtPath(destination).OfType<T>().FirstOrDefault(item => item.name == source.name)
                   ?? AssetDatabase.LoadAssetAtPath<T>(destination);
        }

        private static void CreateCatalog()
        {
            EnsureFolder(SettingsDirectory);
            var archetypes = new List<EnemyArchetypeSpec>();
            foreach ((string _, string targetName) in Prefabs)
            {
                string id = "Enemy." + targetName.Replace("NetworkEnemy", string.Empty).Replace(".prefab", string.Empty);
                string specPath = SettingsDirectory + "/" + targetName.Replace(".prefab", "Spec.asset");
                EnemyArchetypeSpec spec = AssetDatabase.LoadAssetAtPath<EnemyArchetypeSpec>(specPath);
                if (spec == null)
                {
                    spec = ScriptableObject.CreateInstance<EnemyArchetypeSpec>();
                    AssetDatabase.CreateAsset(spec, specPath);
                }
                spec.Configure(id, AssetDatabase.LoadAssetAtPath<GameObject>(TargetDirectory + "/" + targetName));
                EditorUtility.SetDirty(spec);
                archetypes.Add(spec);
            }
            string catalogPath = SettingsDirectory + "/EnemyPresentationCatalog.asset";
            EnemyPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            catalog.Configure(archetypes.ToArray());
            EditorUtility.SetDirty(catalog);
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
