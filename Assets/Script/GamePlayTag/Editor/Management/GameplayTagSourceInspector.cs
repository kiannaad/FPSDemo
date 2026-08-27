using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    [CustomEditor(typeof(GameplayTagSource))]
    public sealed class GameplayTagSourceInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var source = (GameplayTagSource)target;
            EditorGUILayout.HelpBox(
                $"Source: {source.SourceName}\nRoot nodes: {source.Roots.Count}\nDefinitions are read-only here.",
                MessageType.Info);
            if (GUILayout.Button("Open Gameplay Tag Manager"))
            {
                GameplayTagManagerWindow.Open();
            }
        }
    }
}
