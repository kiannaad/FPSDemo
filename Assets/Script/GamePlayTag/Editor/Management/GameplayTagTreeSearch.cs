using System;
using System.Collections.Generic;

namespace CGame.GameplayTags.Editor
{
    public static class GameplayTagTreeSearch
    {
        public static HashSet<string> CollectVisible(GameplayTagRegistrySnapshot snapshot, string query)
        {
            var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (snapshot == null)
            {
                return visible;
            }

            bool showAll = string.IsNullOrWhiteSpace(query);
            foreach (GameplayTagNode root in snapshot.Roots)
            {
                Visit(root, query ?? string.Empty, showAll, visible);
            }

            return visible;
        }

        private static bool Visit(GameplayTagNode node, string query, bool showAll, ISet<string> visible)
        {
            bool directMatch = showAll ||
                               node.SegmentName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                               node.FullName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            if (directMatch)
            {
                AddSubtree(node, visible);
                return true;
            }

            bool childVisible = false;
            foreach (GameplayTagNode child in node.Children)
            {
                childVisible |= Visit(child, query, false, visible);
            }

            if (childVisible)
            {
                visible.Add(node.FullName);
            }

            return childVisible;
        }

        private static void AddSubtree(GameplayTagNode node, ISet<string> visible)
        {
            visible.Add(node.FullName);
            foreach (GameplayTagNode child in node.Children)
            {
                AddSubtree(child, visible);
            }
        }
    }
}
