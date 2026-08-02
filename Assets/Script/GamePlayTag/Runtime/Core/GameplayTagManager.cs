using System;
using System.Collections.Generic;
using System.Linq;

namespace CGame.GameplayTags
{
    public sealed class GameplayTagManager
    {
        private readonly GameplayTagRegistryBuilder registryBuilder = new GameplayTagRegistryBuilder();
        private GameplayTagRegistrySnapshot snapshot;

        private GameplayTagManager()
        {
        }

        public static GameplayTagManager Instance { get; } = new GameplayTagManager();
        public bool IsInitialized => snapshot != null;

        public GameplayTagRegistryBuildResult Initialize(
            IEnumerable<GameplayTagSource> sources,
            IEnumerable<GameplayTagRedirect> redirects = null)
        {
            return ReplaceSnapshotWhenValid(sources, redirects);
        }

        public GameplayTagRegistryBuildResult Rebuild(
            IEnumerable<GameplayTagSource> sources,
            IEnumerable<GameplayTagRedirect> redirects = null)
        {
            EnsureInitialized();
            return ReplaceSnapshotWhenValid(sources, redirects);
        }

        public void Shutdown()
        {
            snapshot = null;
        }

        public GameplayTag RequestTag(string tagName)
        {
            if (!TryRequestTag(tagName, out GameplayTag tag))
            {
                throw new KeyNotFoundException($"GameplayTag '{tagName}' is not registered.");
            }

            return tag;
        }

        public bool TryRequestTag(string tagName, out GameplayTag tag)
        {
            EnsureInitialized();
            if (GameplayTag.TryNormalizeName(tagName, out string normalized) &&
                snapshot.TryGetNode(normalized, out GameplayTagNode node))
            {
                tag = GameplayTag.FromRegisteredName(node.FullName);
                return true;
            }

            tag = GameplayTag.Empty;
            return false;
        }

        public bool TryResolveTag(string tagName, out GameplayTag tag)
        {
            EnsureInitialized();
            if (GameplayTag.TryNormalizeName(tagName, out string normalized) &&
                snapshot.TryResolveName(normalized, out string resolvedName))
            {
                tag = GameplayTag.FromRegisteredName(resolvedName);
                return true;
            }

            tag = GameplayTag.Empty;
            return false;
        }

        public bool IsRegistered(GameplayTag tag)
        {
            EnsureInitialized();
            return !tag.IsEmpty && snapshot.TryGetNode(tag.Name, out _);
        }

        public bool IsExplicitTag(GameplayTag tag)
        {
            EnsureInitialized();
            return !tag.IsEmpty && snapshot.TryGetNode(tag.Name, out GameplayTagNode node) && node.IsExplicitTag;
        }

        public bool TryGetNode(GameplayTag tag, out GameplayTagNode node)
        {
            EnsureInitialized();
            return snapshot.TryGetNode(tag.Name, out node);
        }

        public GameplayTag GetParent(GameplayTag tag)
        {
            GameplayTagNode node = GetRequiredNode(tag);
            return string.IsNullOrEmpty(node.ParentName)
                ? GameplayTag.Empty
                : GameplayTag.FromRegisteredName(node.ParentName);
        }

        public IReadOnlyList<GameplayTag> GetAncestors(GameplayTag tag)
        {
            var ancestors = new List<GameplayTag>();
            GameplayTag current = GetParent(tag);
            while (!current.IsEmpty)
            {
                ancestors.Add(current);
                current = GetParent(current);
            }

            return ancestors;
        }

        public IReadOnlyList<GameplayTag> GetDirectChildren(GameplayTag tag)
        {
            return GetRequiredNode(tag).Children
                .Select(child => GameplayTag.FromRegisteredName(child.FullName))
                .ToArray();
        }

        public IReadOnlyList<GameplayTag> GetDescendants(GameplayTag tag)
        {
            var descendants = new List<GameplayTag>();
            AddDescendants(GetRequiredNode(tag), descendants);
            return descendants;
        }

        public bool MatchesTag(GameplayTag ownedTag, GameplayTag queryTag)
        {
            GameplayTagNode ownedNode = GetRequiredNode(ownedTag);
            GameplayTagNode queryNode = GetRequiredNode(queryTag);
            return NameIsEqualOrDescendant(ownedNode.FullName, queryNode.FullName);
        }

        public IReadOnlyList<GameplayTag> GetExplicitTags()
        {
            EnsureInitialized();
            return snapshot.ExplicitTags;
        }

        private GameplayTagRegistryBuildResult ReplaceSnapshotWhenValid(
            IEnumerable<GameplayTagSource> sources,
            IEnumerable<GameplayTagRedirect> redirects)
        {
            GameplayTagRegistryBuildResult result = registryBuilder.Build(sources, redirects);
            if (result.Succeeded)
            {
                snapshot = result.Snapshot;
            }

            return result;
        }

        private GameplayTagNode GetRequiredNode(GameplayTag tag)
        {
            EnsureInitialized();
            if (tag.IsEmpty || !snapshot.TryGetNode(tag.Name, out GameplayTagNode node))
            {
                throw new ArgumentException($"GameplayTag '{tag.Name}' is not registered.", nameof(tag));
            }

            return node;
        }

        private static bool NameIsEqualOrDescendant(string candidate, string ancestor)
        {
            return candidate.Equals(ancestor, StringComparison.OrdinalIgnoreCase) ||
                   candidate.Length > ancestor.Length &&
                   candidate.StartsWith(ancestor, StringComparison.OrdinalIgnoreCase) &&
                   candidate[ancestor.Length] == '.';
        }

        private static void AddDescendants(GameplayTagNode parent, ICollection<GameplayTag> result)
        {
            foreach (GameplayTagNode child in parent.Children)
            {
                result.Add(GameplayTag.FromRegisteredName(child.FullName));
                AddDescendants(child, result);
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("GameplayTagManager is not initialized.");
            }
        }
    }
}
