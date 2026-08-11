using CGame.Animation.Rig;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor.Rig
{
    [CustomEditor(typeof(KRig))]
    public sealed class KRigEditor : UnityEditor.Editor
    {
        private KRigComponent rigComponent;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            rigComponent = (KRigComponent)EditorGUILayout.ObjectField(
                "Rig Component",
                rigComponent,
                typeof(KRigComponent),
                true);

            using (new EditorGUI.DisabledScope(rigComponent == null))
            {
                if (GUILayout.Button("Import Hierarchy"))
                {
                    KRig rig = (KRig)target;
                    rig.Import(rigComponent);
                    EditorUtility.SetDirty(rig);
                    AssetDatabase.SaveAssetIfDirty(rig);
                }
            }
        }
    }
}
