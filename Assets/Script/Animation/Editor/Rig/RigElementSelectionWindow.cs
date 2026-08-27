using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CGame.Animation.Editor.Rig
{
    public sealed class RigElementSelectionWindow : EditorWindow
    {
        private readonly TreeViewState treeViewState = new TreeViewState();
        private RigElementTreeView treeView;
        private string searchText = string.Empty;

        public static void Open(KRig rig, int currentIndex, string currentName, Action<KRigElement> onSelected)
        {
            if (rig == null)
            {
                throw new ArgumentNullException(nameof(rig));
            }

            RigElementSelectionWindow window = CreateInstance<RigElementSelectionWindow>();
            window.titleContent = new GUIContent("Rig Element Selection");
            window.minSize = new Vector2(450f, 550f);
            window.treeView = new RigElementTreeView(window.treeViewState, selectedElement =>
            {
                onSelected(selectedElement);
                window.Close();
            });
            window.treeView.SetHierarchy(rig.Hierarchy, currentIndex, currentName);
            window.ShowAuxWindow();
        }

        private void OnGUI()
        {
            if (treeView == null)
            {
                EditorGUILayout.HelpBox("No Rig hierarchy is available.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            searchText = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                treeView.Filter(searchText);
            }

            EditorGUILayout.EndHorizontal();
            Rect treeRect = GUILayoutUtility.GetRect(0f, 10000f, 0f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            treeView.OnGUI(treeRect);
        }
    }

    internal sealed class RigElementTreeView : TreeView
    {
        private readonly Action<KRigElement> onSelected;
        private readonly List<TreeViewItem> treeItems = new List<TreeViewItem>();
        private KRigElement[] hierarchy = Array.Empty<KRigElement>();
        private bool suppressSelectionCallback;

        public RigElementTreeView(TreeViewState state, Action<KRigElement> onSelected)
            : base(state)
        {
            this.onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
            showBorder = true;
        }

        public void SetHierarchy(IReadOnlyList<KRigElement> elements, int currentIndex, string currentName)
        {
            hierarchy = new KRigElement[elements.Count];
            for (int index = 0; index < elements.Count; index++)
            {
                hierarchy[index] = elements[index];
            }

            suppressSelectionCallback = true;
            RebuildItems(string.Empty);
            Reload();
            ExpandAll();
            int selectedIndex = FindCurrentIndex(currentIndex, currentName);
            if (selectedIndex >= 0)
            {
                SetSelection(new[] { selectedIndex + 1 });
            }

            suppressSelectionCallback = false;
        }

        public void Filter(string query)
        {
            RebuildItems(query);
            Reload();
            ExpandAll();
        }

        protected override TreeViewItem BuildRoot()
        {
            TreeViewItem root = new TreeViewItem { id = 0, depth = -1, displayName = "Rig" };
            SetupParentsAndChildrenFromDepths(root, treeItems);
            return root;
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (suppressSelectionCallback || selectedIds == null || selectedIds.Count == 0)
            {
                return;
            }

            int selectedIndex = selectedIds[0] - 1;
            if (selectedIndex < 0 || selectedIndex >= hierarchy.Length)
            {
                return;
            }

            onSelected(hierarchy[selectedIndex]);
        }

        private void RebuildItems(string query)
        {
            treeItems.Clear();
            string normalizedQuery = query == null ? string.Empty : query.Trim();
            bool hasQuery = !string.IsNullOrEmpty(normalizedQuery);
            for (int index = 0; index < hierarchy.Length; index++)
            {
                KRigElement element = hierarchy[index];
                if (hasQuery && element.Name.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int depth = hasQuery ? 0 : Mathf.Max(0, element.Depth);
                treeItems.Add(new TreeViewItem(index + 1, depth, element.Name));
            }
        }

        private int FindCurrentIndex(int currentIndex, string currentName)
        {
            if (currentIndex >= 0 && currentIndex < hierarchy.Length)
            {
                return currentIndex;
            }

            for (int index = 0; index < hierarchy.Length; index++)
            {
                if (string.Equals(hierarchy[index].Name, currentName, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
