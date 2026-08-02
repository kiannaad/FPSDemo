using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public sealed class GameplayTagRenameMapping
    {
        public GameplayTagRenameMapping(string oldName, string newName)
        {
            OldName = oldName;
            NewName = newName;
        }

        public string OldName { get; }
        public string NewName { get; }
    }

    public sealed class GameplayTagRenamePlan
    {
        internal GameplayTagRenamePlan(
            GameplayTagConfig config,
            GameplayTagSource source,
            IReadOnlyList<GameplayTagSourceNode> roots,
            IReadOnlyList<GameplayTagRedirect> redirects,
            IReadOnlyList<GameplayTagRenameMapping> mappings)
        {
            Config = config;
            Source = source;
            Roots = roots;
            Redirects = redirects;
            Mappings = mappings;
        }

        public GameplayTagConfig Config { get; }
        public GameplayTagSource Source { get; }
        public IReadOnlyList<GameplayTagRenameMapping> Mappings { get; }
        internal IReadOnlyList<GameplayTagSourceNode> Roots { get; }
        internal IReadOnlyList<GameplayTagRedirect> Redirects { get; }
    }

    public static class GameplayTagRenameService
    {
        private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

        public static bool TryCreatePlan(
            GameplayTagConfig config,
            GameplayTagSource source,
            string oldSubtreeName,
            string newSubtreeName,
            out GameplayTagRenamePlan plan,
            out string error)
        {
            plan = null;
            if (config == null || source == null || !config.Sources.Contains(source))
            {
                error = "A Source registered in the authoritative Config is required.";
                return false;
            }

            if (!GameplayTag.TryCreateSerialized(oldSubtreeName, out GameplayTag oldTag) ||
                !GameplayTag.TryCreateSerialized(newSubtreeName, out GameplayTag newTag))
            {
                error = "Old and new subtree paths must be valid GameplayTag names.";
                return false;
            }

            if (NameComparer.Equals(oldTag.Name, newTag.Name))
            {
                error = "Case-only or unchanged subtree renames are not supported.";
                return false;
            }

            if (newTag.Name.StartsWith(oldTag.Name + ".", StringComparison.OrdinalIgnoreCase))
            {
                error = "A subtree cannot be moved below itself.";
                return false;
            }

            List<NodeDraft> roots = Clone(source.Roots);
            if (!TryDetach(roots, oldTag.Name.Split('.'), out NodeDraft renamedNode))
            {
                error = $"Subtree '{oldTag.Name}' was not found in source '{source.SourceName}'.";
                return false;
            }

            PruneEmptyImplicitNodes(roots);

            var mappings = new List<GameplayTagRenameMapping>();
            CollectExplicitMappings(renamedNode, oldTag.Name, newTag.Name, mappings);

            string[] newSegments = newTag.Name.Split('.');
            List<NodeDraft> destination = roots;
            for (int index = 0; index < newSegments.Length - 1; index++)
            {
                NodeDraft parent = destination.FirstOrDefault(node => NameComparer.Equals(node.SegmentName, newSegments[index]));
                if (parent == null)
                {
                    parent = new NodeDraft(newSegments[index]);
                    destination.Add(parent);
                }

                destination = parent.Children;
            }

            string newSegment = newSegments[newSegments.Length - 1];
            if (destination.Any(node => NameComparer.Equals(node.SegmentName, newSegment)))
            {
                error = $"Destination subtree '{newTag.Name}' already exists.";
                return false;
            }

            renamedNode.SegmentName = newSegment;
            destination.Add(renamedNode);
            GameplayTagSourceNode[] proposedRoots = ToSourceNodes(roots);
            GameplayTagRedirect[] proposedRedirects = config.Redirects
                .Concat(mappings.Select(mapping => new GameplayTagRedirect(mapping.OldName, mapping.NewName)))
                .ToArray();

            GameplayTagSource temporarySource = ScriptableObject.CreateInstance<GameplayTagSource>();
            try
            {
                temporarySource.SetDefinition(source.SourceName, proposedRoots);
                GameplayTagSource[] proposedSources = config.Sources
                    .Select(item => item == source ? temporarySource : item)
                    .ToArray();
                GameplayTagRegistryBuildResult result = new GameplayTagRegistryBuilder().Build(proposedSources, proposedRedirects);
                if (!result.Succeeded)
                {
                    error = string.Join("\n", result.Errors.Select(item => item.ToString()));
                    return false;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporarySource);
            }

            plan = new GameplayTagRenamePlan(config, source, proposedRoots, proposedRedirects, mappings);
            error = string.Empty;
            return true;
        }

        public static bool Apply(GameplayTagRenamePlan plan, out string error)
        {
            if (plan?.Config == null || plan.Source == null || !plan.Config.Sources.Contains(plan.Source))
            {
                error = "The rename preview is stale; rebuild it before applying.";
                return false;
            }

            plan.Source.SetDefinition(plan.Source.SourceName, plan.Roots);
            plan.Config.SetDefinition(plan.Config.Sources, plan.Redirects);
            EditorUtility.SetDirty(plan.Source);
            EditorUtility.SetDirty(plan.Config);
            if (PrefabUtility.IsPartOfPrefabAsset(plan.Config.gameObject))
            {
                PrefabUtility.SavePrefabAsset(plan.Config.gameObject);
            }
            AssetDatabase.SaveAssets();
            GameplayTagPickerModel.InvalidateAuthoritativeConfigCache();
            error = string.Empty;
            return true;
        }

        public static IReadOnlyList<string> CollectExplicitNames(GameplayTagSource source, string subtreeName = null)
        {
            if (source == null)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            foreach (GameplayTagSourceNode root in source.Roots)
            {
                CollectExplicitNames(root, string.Empty, subtreeName, result);
            }

            return result;
        }

        private static void CollectExplicitNames(
            GameplayTagSourceNode node,
            string parentName,
            string subtreeName,
            ICollection<string> result)
        {
            string fullName = string.IsNullOrEmpty(parentName) ? node.SegmentName : $"{parentName}.{node.SegmentName}";
            bool insideRequestedSubtree = string.IsNullOrEmpty(subtreeName) ||
                                          NameComparer.Equals(fullName, subtreeName) ||
                                          fullName.StartsWith(subtreeName + ".", StringComparison.OrdinalIgnoreCase);
            if (insideRequestedSubtree && node.IsExplicitTag)
            {
                result.Add(fullName);
            }

            foreach (GameplayTagSourceNode child in node.Children)
            {
                CollectExplicitNames(child, fullName, subtreeName, result);
            }
        }

        private static bool TryDetach(IList<NodeDraft> roots, IReadOnlyList<string> segments, out NodeDraft detached)
        {
            IList<NodeDraft> siblings = roots;
            for (int index = 0; index < segments.Count; index++)
            {
                NodeDraft node = siblings.FirstOrDefault(item => NameComparer.Equals(item.SegmentName, segments[index]));
                if (node == null)
                {
                    detached = null;
                    return false;
                }

                if (index == segments.Count - 1)
                {
                    siblings.Remove(node);
                    detached = node;
                    return true;
                }

                siblings = node.Children;
            }

            detached = null;
            return false;
        }

        private static void PruneEmptyImplicitNodes(IList<NodeDraft> nodes)
        {
            for (int index = nodes.Count - 1; index >= 0; index--)
            {
                NodeDraft node = nodes[index];
                PruneEmptyImplicitNodes(node.Children);
                if (!node.IsExplicitTag && node.Children.Count == 0)
                {
                    nodes.RemoveAt(index);
                }
            }
        }

        private static void CollectExplicitMappings(
            NodeDraft node,
            string oldName,
            string newName,
            ICollection<GameplayTagRenameMapping> mappings)
        {
            if (node.IsExplicitTag)
            {
                mappings.Add(new GameplayTagRenameMapping(oldName, newName));
            }

            foreach (NodeDraft child in node.Children)
            {
                CollectExplicitMappings(
                    child,
                    $"{oldName}.{child.SegmentName}",
                    $"{newName}.{child.SegmentName}",
                    mappings);
            }
        }

        private static List<NodeDraft> Clone(IEnumerable<GameplayTagSourceNode> nodes)
        {
            return (nodes ?? Array.Empty<GameplayTagSourceNode>())
                .Where(node => node != null)
                .Select(node => new NodeDraft(node))
                .ToList();
        }

        private static GameplayTagSourceNode[] ToSourceNodes(IEnumerable<NodeDraft> nodes)
        {
            return nodes
                .OrderBy(node => node.SegmentName, NameComparer)
                .ThenBy(node => node.SegmentName, StringComparer.Ordinal)
                .Select(node => node.ToSourceNode())
                .ToArray();
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

            public string SegmentName { get; set; }
            public bool IsExplicitTag { get; }
            public string Comment { get; } = string.Empty;
            public List<NodeDraft> Children { get; private set; } = new List<NodeDraft>();

            public GameplayTagSourceNode ToSourceNode()
            {
                return new GameplayTagSourceNode(SegmentName, IsExplicitTag, Comment, ToSourceNodes(Children));
            }
        }
    }
}
