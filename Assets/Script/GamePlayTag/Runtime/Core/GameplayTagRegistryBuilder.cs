using System;
using System.Collections.Generic;
using System.Linq;

namespace CGame.GameplayTags
{
    public sealed class GameplayTagRegistryBuilder
    {
        private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

        public GameplayTagRegistryBuildResult Build(
            IEnumerable<GameplayTagSource> sources,
            IEnumerable<GameplayTagRedirect> redirects = null)
        {
            var errors = new List<GameplayTagRegistryError>();
            var mutableRoots = new Dictionary<string, MutableNode>(NameComparer);
            var mutableNodes = new Dictionary<string, MutableNode>(NameComparer);
            GameplayTagSource[] inputSources = (sources ?? Enumerable.Empty<GameplayTagSource>()).ToArray();
            GameplayTagSource[] orderedSources = inputSources
                .Where(source => source != null)
                .OrderBy(source => source.SourceName, NameComparer)
                .ThenBy(source => source.SourceName, StringComparer.Ordinal)
                .ToArray();

            ValidateSourcesExist(sources, inputSources, orderedSources, errors);
            ValidateUniqueSourceNames(orderedSources, errors);

            foreach (GameplayTagSource source in orderedSources)
            {
                if (string.IsNullOrWhiteSpace(source.SourceName))
                {
                    errors.Add(new GameplayTagRegistryError("InvalidSourceName", "A GameplayTag source has an empty source name."));
                    continue;
                }

                MergeSiblings(source.Roots, null, source.SourceName, mutableRoots, mutableNodes, errors);
            }

            if (errors.Count > 0)
            {
                return Failed(errors);
            }

            var frozenNodes = new Dictionary<string, GameplayTagNode>(NameComparer);
            IReadOnlyList<GameplayTagNode> frozenRoots = FreezeChildren(mutableRoots.Values, frozenNodes);
            var explicitTags = frozenNodes.Values
                .Where(node => node.IsExplicitTag)
                .OrderBy(node => node.FullName, NameComparer)
                .ThenBy(node => node.FullName, StringComparer.Ordinal)
                .Select(node => GameplayTag.FromRegisteredName(node.FullName))
                .ToArray();

            Dictionary<string, string> resolvedRedirects = BuildRedirects(redirects, frozenNodes, errors);
            if (errors.Count > 0)
            {
                return Failed(errors);
            }

            var snapshot = new GameplayTagRegistrySnapshot(
                frozenNodes,
                resolvedRedirects,
                frozenRoots,
                Array.AsReadOnly(explicitTags));
            return new GameplayTagRegistryBuildResult(snapshot, Array.Empty<GameplayTagRegistryError>());
        }

        private static void ValidateSourcesExist(
            IEnumerable<GameplayTagSource> sourceEnumerable,
            IReadOnlyCollection<GameplayTagSource> inputSources,
            IReadOnlyCollection<GameplayTagSource> orderedSources,
            ICollection<GameplayTagRegistryError> errors)
        {
            if (sourceEnumerable == null || orderedSources.Count == 0)
            {
                errors.Add(new GameplayTagRegistryError("MissingSources", "At least one GameplayTag source is required."));
            }

            if (inputSources.Any(source => source == null))
            {
                errors.Add(new GameplayTagRegistryError("NullSource", "GameplayTag source collections cannot contain null entries."));
            }
        }

        private static void ValidateUniqueSourceNames(
            IEnumerable<GameplayTagSource> sources,
            ICollection<GameplayTagRegistryError> errors)
        {
            var names = new HashSet<string>(NameComparer);
            foreach (GameplayTagSource source in sources)
            {
                if (!string.IsNullOrWhiteSpace(source.SourceName) && !names.Add(source.SourceName))
                {
                    errors.Add(new GameplayTagRegistryError(
                        "DuplicateSource",
                        $"GameplayTag source name '{source.SourceName}' is registered more than once."));
                }
            }
        }

        private static void MergeSiblings(
            IReadOnlyList<GameplayTagSourceNode> sourceNodes,
            MutableNode parent,
            string sourceName,
            Dictionary<string, MutableNode> targetSiblings,
            Dictionary<string, MutableNode> allNodes,
            ICollection<GameplayTagRegistryError> errors)
        {
            var siblingNames = new HashSet<string>(NameComparer);
            foreach (GameplayTagSourceNode sourceNode in sourceNodes ?? Array.Empty<GameplayTagSourceNode>())
            {
                if (sourceNode == null)
                {
                    errors.Add(new GameplayTagRegistryError("NullNode", $"Source '{sourceName}' contains a null node."));
                    continue;
                }

                if (!GameplayTag.IsValidSegment(sourceNode.SegmentName))
                {
                    errors.Add(new GameplayTagRegistryError(
                        "InvalidSegment",
                        $"Source '{sourceName}' contains invalid segment '{sourceNode.SegmentName}'."));
                    continue;
                }

                if (!siblingNames.Add(sourceNode.SegmentName))
                {
                    errors.Add(new GameplayTagRegistryError(
                        "DuplicateSibling",
                        $"Source '{sourceName}' contains duplicate sibling segment '{sourceNode.SegmentName}'."));
                    continue;
                }

                string requestedFullName = parent == null
                    ? sourceNode.SegmentName
                    : $"{parent.FullName}.{sourceNode.SegmentName}";

                if (!targetSiblings.TryGetValue(sourceNode.SegmentName, out MutableNode targetNode))
                {
                    targetNode = new MutableNode(sourceNode.SegmentName, requestedFullName, parent?.FullName);
                    targetSiblings.Add(sourceNode.SegmentName, targetNode);
                    allNodes.Add(targetNode.FullName, targetNode);
                }

                if (sourceNode.IsExplicitTag)
                {
                    if (targetNode.IsExplicitTag)
                    {
                        errors.Add(new GameplayTagRegistryError(
                            "DuplicateExplicitTag",
                            $"Explicit tag '{targetNode.FullName}' is owned by both '{targetNode.ExplicitSourceName}' and '{sourceName}'."));
                    }
                    else
                    {
                        targetNode.IsExplicitTag = true;
                        targetNode.ExplicitSourceName = sourceName;
                    }
                }

                MergeSiblings(sourceNode.Children, targetNode, sourceName, targetNode.Children, allNodes, errors);
            }
        }

        private static IReadOnlyList<GameplayTagNode> FreezeChildren(
            IEnumerable<MutableNode> mutableNodes,
            Dictionary<string, GameplayTagNode> frozenNodes)
        {
            var result = new List<GameplayTagNode>();
            foreach (MutableNode mutableNode in mutableNodes
                         .OrderBy(node => node.SegmentName, NameComparer)
                         .ThenBy(node => node.SegmentName, StringComparer.Ordinal))
            {
                IReadOnlyList<GameplayTagNode> children = FreezeChildren(mutableNode.Children.Values, frozenNodes);
                var frozenNode = new GameplayTagNode(
                    mutableNode.SegmentName,
                    mutableNode.FullName,
                    mutableNode.ParentName,
                    mutableNode.IsExplicitTag,
                    mutableNode.ExplicitSourceName,
                    children);
                frozenNodes.Add(frozenNode.FullName, frozenNode);
                result.Add(frozenNode);
            }

            return result.AsReadOnly();
        }

        private static Dictionary<string, string> BuildRedirects(
            IEnumerable<GameplayTagRedirect> redirects,
            IReadOnlyDictionary<string, GameplayTagNode> nodes,
            ICollection<GameplayTagRegistryError> errors)
        {
            var direct = new Dictionary<string, string>(NameComparer);
            var directTargets = new HashSet<string>(NameComparer);
            foreach (GameplayTagRedirect redirect in redirects ?? Enumerable.Empty<GameplayTagRedirect>())
            {
                if (redirect == null || !GameplayTag.TryNormalizeName(redirect.OldName, out string oldName) ||
                    !GameplayTag.TryNormalizeName(redirect.NewName, out string newName))
                {
                    errors.Add(new GameplayTagRegistryError("InvalidRedirect", "A GameplayTag redirect contains an invalid old or new name."));
                    continue;
                }

                if (nodes.ContainsKey(oldName))
                {
                    errors.Add(new GameplayTagRegistryError("RedirectOldNameRegistered", $"Redirect old name '{oldName}' is still registered."));
                }
                else if (direct.ContainsKey(oldName))
                {
                    errors.Add(new GameplayTagRegistryError("DuplicateRedirect", $"Redirect old name '{oldName}' is declared more than once."));
                }
                else if (!directTargets.Add(newName))
                {
                    errors.Add(new GameplayTagRegistryError("RedirectTargetConflict", $"Multiple redirects target '{newName}'."));
                }
                else
                {
                    direct.Add(oldName, newName);
                }
            }

            var resolved = new Dictionary<string, string>(NameComparer);
            foreach (string oldName in direct.Keys)
            {
                var visited = new HashSet<string>(NameComparer);
                string current = oldName;
                while (direct.TryGetValue(current, out string next))
                {
                    if (!visited.Add(current))
                    {
                        errors.Add(new GameplayTagRegistryError("RedirectCycle", $"Redirect chain for '{oldName}' contains a cycle."));
                        current = null;
                        break;
                    }

                    current = next;
                }

                if (current == null)
                {
                    continue;
                }

                if (!nodes.TryGetValue(current, out GameplayTagNode targetNode) || !targetNode.IsExplicitTag)
                {
                    errors.Add(new GameplayTagRegistryError("RedirectTargetMissing", $"Redirect '{oldName}' does not resolve to an explicit tag."));
                    continue;
                }

                resolved[oldName] = targetNode.FullName;
            }

            return resolved;
        }

        private static GameplayTagRegistryBuildResult Failed(IReadOnlyList<GameplayTagRegistryError> errors)
        {
            return new GameplayTagRegistryBuildResult(null, errors.ToArray());
        }

        private sealed class MutableNode
        {
            public MutableNode(string segmentName, string fullName, string parentName)
            {
                SegmentName = segmentName;
                FullName = fullName;
                ParentName = parentName ?? string.Empty;
            }

            public string SegmentName { get; }
            public string FullName { get; }
            public string ParentName { get; }
            public bool IsExplicitTag { get; set; }
            public string ExplicitSourceName { get; set; }
            public Dictionary<string, MutableNode> Children { get; } = new Dictionary<string, MutableNode>(NameComparer);
        }
    }
}
