using System;
using System.Collections.Generic;

namespace CGame.GameplayTags
{
    public sealed class GameplayTagRegistrySnapshot
    {
        private readonly Dictionary<string, GameplayTagNode> nodesByName;
        private readonly Dictionary<string, string> resolvedRedirects;

        internal GameplayTagRegistrySnapshot(
            Dictionary<string, GameplayTagNode> nodesByName,
            Dictionary<string, string> resolvedRedirects,
            IReadOnlyList<GameplayTagNode> roots,
            IReadOnlyList<GameplayTag> explicitTags)
        {
            this.nodesByName = nodesByName;
            this.resolvedRedirects = resolvedRedirects;
            Roots = roots;
            ExplicitTags = explicitTags;
        }

        public IReadOnlyList<GameplayTagNode> Roots { get; }
        public IReadOnlyList<GameplayTag> ExplicitTags { get; }

        public bool TryGetNode(string tagName, out GameplayTagNode node)
        {
            return nodesByName.TryGetValue(tagName ?? string.Empty, out node);
        }

        public bool TryResolveName(string tagName, out string resolvedName)
        {
            if (nodesByName.TryGetValue(tagName ?? string.Empty, out GameplayTagNode node))
            {
                resolvedName = node.FullName;
                return true;
            }

            return resolvedRedirects.TryGetValue(tagName ?? string.Empty, out resolvedName);
        }
    }
}
