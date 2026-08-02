using System.Collections.Generic;

namespace CGame.GameplayTags
{
    public sealed class GameplayTagNode
    {
        internal GameplayTagNode(
            string segmentName,
            string fullName,
            string parentName,
            bool isExplicitTag,
            string explicitSourceName,
            IReadOnlyList<GameplayTagNode> children)
        {
            SegmentName = segmentName;
            FullName = fullName;
            ParentName = parentName;
            IsExplicitTag = isExplicitTag;
            ExplicitSourceName = explicitSourceName ?? string.Empty;
            Children = children;
        }

        public string SegmentName { get; }
        public string FullName { get; }
        public string ParentName { get; }
        public bool IsExplicitTag { get; }
        public string ExplicitSourceName { get; }
        public IReadOnlyList<GameplayTagNode> Children { get; }
    }
}
