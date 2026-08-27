using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.GameplayTags
{
    [Serializable]
    public sealed class GameplayTagSourceNode
    {
        [SerializeField] private string segmentName;
        [SerializeField] private bool isExplicitTag;
        [SerializeField] private string devComment;
        [SerializeReference] private List<GameplayTagSourceNode> children = new List<GameplayTagSourceNode>();

        public GameplayTagSourceNode(string segmentName, bool isExplicitTag = false, string devComment = null, IEnumerable<GameplayTagSourceNode> children = null)
        {
            this.segmentName = segmentName;
            this.isExplicitTag = isExplicitTag;
            this.devComment = devComment ?? string.Empty;
            if (children != null)
            {
                this.children.AddRange(children);
            }
        }

        public string SegmentName => segmentName;
        public bool IsExplicitTag => isExplicitTag;
        public string DevComment => devComment ?? string.Empty;
        public IReadOnlyList<GameplayTagSourceNode> Children => children.AsReadOnly();
    }
}
