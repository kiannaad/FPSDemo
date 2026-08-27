using System;
using UnityEngine;

namespace GAS.TagSystem
{
    /// <summary>
    /// 标签系统 以String表示标签 在后续GAS之中进行条件判断
    /// </summary>
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField]
        [GasTag]
        private string tagName;

        public string TagName => tagName;

        /// <summary>
        /// 构造函数 赋值Tag
        /// </summary>
        /// <param name="tagName">标签名字</param>
        public GameplayTag(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                throw new ArgumentException("Tag name cannot be null!", nameof(tagName));
            this.tagName = tagName;
        }

        /// <summary>
        /// 内部构造 允许空标签
        /// </summary>
        private GameplayTag(string tagName, bool allowEmpty)
        {
            this.tagName = allowEmpty ? (tagName ?? string.Empty) : tagName;
        }

        public static readonly GameplayTag EmptyTag = new GameplayTag(string.Empty, true);

        public string[] TagNameSplit => string.IsNullOrEmpty(tagName)
            ? Array.Empty<string>()
            : tagName.Split('.');

        public int Depth => TagNameSplit.Length;

        public bool Equals(GameplayTag other) => tagName == other.tagName;

        public override bool Equals(object obj) => obj is GameplayTag tag && Equals(tag);

        public override int GetHashCode() => tagName != null ? tagName.GetHashCode() : 0;

        public override string ToString() => tagName;

        /// <summary>
        /// 检查此标签是否是目标标签的父标签
        /// 比如 Ailment (this) 是 Ailment.Poison (other)的父标签
        /// </summary>
        public bool IsParentOf(GameplayTag otherTag)
        {
            var thisSplitArray = TagNameSplit;
            var otherSplitArray = otherTag.TagNameSplit;

            //如果此标签的层级深度大于等于目标标签的层级深度 则返回false
            //this = 2 other=1 那肯定是other才是父信息
            if (thisSplitArray.Length >= otherSplitArray.Length) return false;

            for (int i = 0; i < thisSplitArray.Length; i++)
            {
                if (thisSplitArray[i] != otherSplitArray[i]) return false;
            }
            return true;
        }
    }
}
