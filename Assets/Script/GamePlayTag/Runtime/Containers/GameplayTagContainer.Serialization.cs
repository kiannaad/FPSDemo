using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.GameplayTags
{
    public sealed partial class GameplayTagContainer : ISerializationCallbackReceiver
    {
        [SerializeField] private List<GameplayTag> serializedTags = new List<GameplayTag>();
        [NonSerialized] private HashSet<GameplayTag> tagSet;

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            RebuildCache();
        }

        private void EnsureCache()
        {
            if (tagSet == null)
            {
                RebuildCache();
            }
        }

        private void RebuildCache()
        {
            tagSet = new HashSet<GameplayTag>();
            var uniqueTags = new List<GameplayTag>();
            foreach (GameplayTag tag in serializedTags ?? new List<GameplayTag>())
            {
                if (!tag.IsEmpty && tagSet.Add(tag))
                {
                    uniqueTags.Add(tag);
                }
            }

            serializedTags = uniqueTags;
        }
    }
}
