using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GAS.Demo;

namespace GAS.Editor.Demo
{
    /// <summary>
    /// 一键生成 GAS Demo 场景物体
    /// </summary>
    public static class GasDemoSceneMenu
    {
        /// <summary> Demo 场景路径 </summary>
        private const string ScenePath = "Assets/_Scripts/GAS/Demo/GasDemoScene.unity";

        /// <summary>
        /// 在当前场景创建 Demo 物体
        /// </summary>
        [MenuItem("Tools/MmGAS/Create GAS Demo Object", priority = 100)]
        public static void CreateDemoObject()
        {
            GameObject root = new GameObject("GAS_Demo");
            root.AddComponent<GasDemoController>();
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[MmGAS] 已创建 GAS_Demo 物体 直接 Play 即可");
        }

        /// <summary>
        /// 新建并打开 Demo 场景
        /// </summary>
        [MenuItem("Tools/MmGAS/Open GAS Demo Scene", priority = 101)]
        public static void OpenOrCreateDemoScene()
        {
            EnsureFolder("Assets/_Scripts/GAS/Demo");

            Scene scene;
            if (System.IO.File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                GameObject root = new GameObject("GAS_Demo");
                root.AddComponent<GasDemoController>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }

            GameObject existing = GameObject.Find("GAS_Demo");
            if (existing == null)
            {
                existing = new GameObject("GAS_Demo");
                existing.AddComponent<GasDemoController>();
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Selection.activeGameObject = existing;
            Debug.Log($"[MmGAS] Demo 场景就绪 {ScenePath}");
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] partList = folderPath.Split('/');
            string current = partList[0];
            for (int i = 1; i < partList.Length; i++)
            {
                string next = current + "/" + partList[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, partList[i]);
                current = next;
            }
        }
    }
}
