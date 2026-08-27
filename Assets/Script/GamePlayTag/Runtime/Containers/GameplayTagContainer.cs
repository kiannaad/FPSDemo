using System;
using System.Collections.Generic;
using System.Linq;

namespace CGame.GameplayTags
{
    [Serializable]
    public sealed partial class GameplayTagContainer
    {
        public GameplayTagContainer()
        {
        }

        public GameplayTagContainer(GameplayTagContainer source)
        {
            if (source == null)
            {
                return;
            }

            serializedTags = new List<GameplayTag>(source.Tags);
            RebuildCache();
        }

        public int Count
        {
            get
            {
                EnsureCache();
                return tagSet.Count;
            }
        }

        public IReadOnlyList<GameplayTag> Tags => serializedTags.AsReadOnly();

        public bool AddTag(GameplayTag tag)
        {
            EnsureCache();
            if (tag.IsEmpty || !GameplayTagManager.Instance.IsExplicitTag(tag) || !tagSet.Add(tag))
            {
                return false;
            }

            serializedTags.Add(GameplayTagManager.Instance.RequestTag(tag.Name));
            return true;
        }

        public bool RemoveTag(GameplayTag tag)
        {
            EnsureCache();
            if (!tagSet.Remove(tag))
            {
                return false;
            }

            int index = serializedTags.FindIndex(item => item == tag);
            if (index >= 0)
            {
                serializedTags.RemoveAt(index);
            }

            return true;
        }

        public bool HasTagExact(GameplayTag tag)
        {
            EnsureCache();
            return tagSet.Contains(tag);
        }

        public bool HasTag(GameplayTag queryTag)
        {
            EnsureCache();
            return tagSet.Any(ownedTag => GameplayTagManager.Instance.MatchesTag(ownedTag, queryTag));
        }

        public bool HasAny(IEnumerable<GameplayTag> queryTags, bool exact = false)
        {
            if (queryTags == null)
            {
                return false;
            }

            return queryTags.Any(queryTag => exact ? HasTagExact(queryTag) : HasTag(queryTag));
        }

        public bool HasAll(IEnumerable<GameplayTag> queryTags, bool exact = false)
        {
            if (queryTags == null)
            {
                return false;
            }

            return queryTags.All(queryTag => exact ? HasTagExact(queryTag) : HasTag(queryTag));
        }

        public GameplayTagContainer Copy()
        {
            return new GameplayTagContainer(this);
        }
    }
}
