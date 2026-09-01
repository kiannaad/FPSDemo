using CGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CGame.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            LevelDefinition definition = (LevelDefinition)target;
            var scene = EditorSceneManager.GetActiveScene();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                LevelDefinitionScanner.IsSnapshotCurrent(scene, definition) ? "Spawn point snapshot is current." : "Spawn point snapshot is missing or stale.",
                MessageType.Info);
            if (GUILayout.Button("Add Player Point")) LevelDefinitionScanner.AddPointAndScan(scene, definition, SpawnPointKind.Player);
            if (GUILayout.Button("Add Enemy Point")) LevelDefinitionScanner.AddPointAndScan(scene, definition, SpawnPointKind.Enemy);
            if (GUILayout.Button("Scan Now")) LevelDefinitionScanner.ScanAndApply(scene, definition);
        }
    }
}
