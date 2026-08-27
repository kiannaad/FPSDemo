using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace CGame.GameplayTags.Editor
{
    public static class GameplayTagSourceMutationService
    {
        private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

        public static bool AddTagPath(GameplayTagSource source, string fullName, string comment, out string error)
        {
            if (source == null)
            {
                error = "A source must be selected.";
                return false;
            }

            if (!GameplayTag.TryCreateSerialized(fullName, out GameplayTag tag))
            {
                error = "Tag paths use ASCII letters, digits or underscores separated by single dots.";
                return false;
            }

            List<NodeDraft> roots = Clone(source.Roots);
            List<NodeDraft> siblings = roots;
            string[] segments = tag.Name.Split('.');
            for (int index = 0; index < segments.Length; index++)
            {
                NodeDraft node = siblings.FirstOrDefault(item => NameComparer.Equals(item.SegmentName, segments[index]));
                if (node == null)
                {
                    node = new NodeDraft(segments[index]);
                    siblings.Add(node);
                }

                if (index == segments.Length - 1)
                {
                    if (node.IsExplicitTag)
                    {
                        error = $"Tag '{tag.Name}' already exists in source '{source.SourceName}'.";
                        return false;
                    }

                    node.IsExplicitTag = true;
                    node.Comment = comment ?? string.Empty;
                }

                siblings = node.Children;
            }

            Apply(source, roots);
            error = string.Empty;
            return true;
        }

        public static bool DeleteSubtree(GameplayTagSource source, string fullName, out string error)
        {
            if (source == null || !GameplayTag.TryCreateSerialized(fullName, out GameplayTag tag))
            {
                error = "A valid source and subtree path are required.";
                return false;
            }

            List<NodeDraft> roots = Clone(source.Roots);
            List<NodeDraft> siblings = roots;
            string[] segments = tag.Name.Split('.');
            for (int index = 0; index < segments.Length; index++)
            {
                NodeDraft node = siblings.FirstOrDefault(item => NameComparer.Equals(item.SegmentName, segments[index]));
                if (node == null)
                {
                    error = $"Subtree '{tag.Name}' was not found in source '{source.SourceName}'.";
                    return false;
                }

                if (index == segments.Length - 1)
                {
                    siblings.Remove(node);
                    Apply(source, roots);
                    error = string.Empty;
                    return true;
                }

                siblings = node.Children;
            }

            error = $"Subtree '{tag.Name}' was not found.";
            return false;
        }

        public static bool DeleteSubtreeSafely(
            GameplayTagConfig config,
            GameplayTagSource source,
            string fullName,
            out string error)
        {
            if (config == null)
            {
                error = "The authoritative GameplayTagConfig is required before deleting a subtree.";
                return false;
            }

            IReadOnlyList<string> explicitNames = GameplayTagRenameService.CollectExplicitNames(source, fullName);
            GameplayTagAssetScanReport report = GameplayTagAssetScanner.ScanProject(config);
            if (report.ConfigurationErrors.Count > 0)
            {
                error = string.Join("\n", report.ConfigurationErrors);
                return false;
            }

            var requested = new HashSet<string>(explicitNames, NameComparer);
            GameplayTagAssetReference reference = report.References.FirstOrDefault(item =>
                requested.Contains(item.RawName) || requested.Contains(item.ResolvedName));
            if (reference != null)
            {
                error = $"Subtree deletion is blocked by {reference.AssetPath}:{reference.PropertyPath}='{reference.RawName}'.";
                return false;
            }

            return DeleteSubtree(source, fullName, out error);
        }

        private static List<NodeDraft> Clone(IEnumerable<GameplayTagSourceNode> nodes)
        {
            return (nodes ?? Array.Empty<GameplayTagSourceNode>())
                .Where(node => node != null)
                .Select(node => new NodeDraft(node))
                .ToList();
        }

        private static void Apply(GameplayTagSource source, IEnumerable<NodeDraft> roots)
        {
            source.SetDefinition(source.SourceName, roots
                .OrderBy(node => node.SegmentName, NameComparer)
                .ThenBy(node => node.SegmentName, StringComparer.Ordinal)
                .Select(node => node.ToSourceNode()));
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
        }

        private sealed class NodeDraft
        {
            public NodeDraft(string segmentName)
            {
                SegmentName = segmentName;
            }

            public NodeDraft(GameplayTagSourceNode source)
            {
                SegmentName = source.SegmentName;
                IsExplicitTag = source.IsExplicitTag;
                Comment = source.DevComment;
                Children = Clone(source.Children);
            }

            public string SegmentName { get; }
            public bool IsExplicitTag { get; set; }
            public string Comment { get; set; } = string.Empty;
            public List<NodeDraft> Children { get; private set; } = new List<NodeDraft>();

            public GameplayTagSourceNode ToSourceNode()
            {
                return new GameplayTagSourceNode(
                    SegmentName,
                    IsExplicitTag,
                    Comment,
                    Children
                        .OrderBy(node => node.SegmentName, NameComparer)
                        .ThenBy(node => node.SegmentName, StringComparer.Ordinal)
                        .Select(node => node.ToSourceNode()));
            }
        }
    }
}
