using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public sealed class GameplayTagManagerTreeView : TreeView
    {
        private GameplayTagRegistrySnapshot snapshot;
        private string sourceFilter = string.Empty;
        private string filterSearch = string.Empty;
        private readonly Dictionary<int, GameplayTagNode> nodesById = new Dictionary<int, GameplayTagNode>();

        public GameplayTagManagerTreeView(TreeViewState state) : base(state)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = 20f;
            Reload();
        }

        public string SelectedTagName
        {
            get
            {
                IList<int> selection = GetSelection();
                return selection.Count == 1 && nodesById.TryGetValue(selection[0], out GameplayTagNode node)
                    ? node.FullName
                    : string.Empty;
            }
        }

        public bool HasVisibleRows => nodesById.Count > 0;

        public void SetData(GameplayTagRegistrySnapshot newSnapshot, string search, string newSourceFilter)
        {
            snapshot = newSnapshot;
            filterSearch = search ?? string.Empty;
            searchString = string.Empty;
            sourceFilter = newSourceFilter ?? string.Empty;
            Reload();
            if (!string.IsNullOrEmpty(filterSearch))
            {
                SetExpanded(nodesById.Keys.ToList());
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            nodesById.Clear();
            var root = new TreeViewItem(0, -1, "Root");
            if (snapshot == null)
            {
                root.children = new List<TreeViewItem>();
                return root;
            }

            HashSet<string> visible = GameplayTagTreeSearch.CollectVisible(snapshot, filterSearch);
            int nextId = 1;
            foreach (GameplayTagNode node in snapshot.Roots)
            {
                AddNode(root, node, visible, ref nextId);
            }

            if (!root.hasChildren)
            {
                root.children = new List<TreeViewItem>();
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (!nodesById.TryGetValue(args.item.id, out GameplayTagNode node))
            {
                base.RowGUI(args);
                return;
            }

            Rect rect = args.rowRect;
            rect.x += GetContentIndent(args.item);
            rect.width -= GetContentIndent(args.item);
            string marker = node.IsExplicitTag ? string.Empty : "  (implicit)";
            GUIStyle style = node.IsExplicitTag ? EditorStyles.label : EditorStyles.miniLabel;
            EditorGUI.LabelField(rect, node.SegmentName + marker, style);

            if (node.IsExplicitTag && !string.IsNullOrEmpty(node.ExplicitSourceName))
            {
                var sourceRect = new Rect(rect.xMax - 190f, rect.y, 185f, rect.height);
                EditorGUI.LabelField(sourceRect, node.ExplicitSourceName, EditorStyles.miniLabel);
            }
        }

        private bool AddNode(TreeViewItem parent, GameplayTagNode node, ISet<string> visible, ref int nextId)
        {
            bool sourceMatches = string.IsNullOrEmpty(sourceFilter) || NodeContainsSource(node, sourceFilter);
            if (!visible.Contains(node.FullName) || !sourceMatches)
            {
                return false;
            }

            int id = nextId++;
            var item = new TreeViewItem(id, parent.depth + 1, node.SegmentName);
            nodesById[id] = node;
            parent.AddChild(item);
            foreach (GameplayTagNode child in node.Children)
            {
                AddNode(item, child, visible, ref nextId);
            }

            return true;
        }

        private static bool NodeContainsSource(GameplayTagNode node, string sourceName)
        {
            if (node.IsExplicitTag && node.ExplicitSourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return node.Children.Any(child => NodeContainsSource(child, sourceName));
        }
    }
}
