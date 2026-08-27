using System;
using System.Collections.Generic;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame.Ability.Cues
{
    [CreateAssetMenu(fileName = "GameplayCueSet", menuName = "CGame/Ability/Gameplay Cue Set")]
    public sealed class GameplayCueSet : ScriptableObject
    {
        [SerializeField] private int priority;
        [SerializeField] private List<GameplayCueSetEntry> entries = new List<GameplayCueSetEntry>();

        public int Priority => priority;
        public IReadOnlyList<GameplayCueSetEntry> Entries => entries;

        public void SetDefinition(int newPriority, IEnumerable<GameplayCueSetEntry> newEntries)
        {
            priority = newPriority;
            entries.Clear();
            if (newEntries != null)
            {
                entries.AddRange(newEntries);
            }
        }
    }

    [Serializable]
    public sealed class GameplayCueSetEntry
    {
        [SerializeField] private GameplayTag cueTag;
        [SerializeField] private CueNotifyDefinition[] notifies = Array.Empty<CueNotifyDefinition>();

        public GameplayTag CueTag => cueTag;
        public IReadOnlyList<CueNotifyDefinition> Notifies => notifies;

        public void SetDefinition(GameplayTag newCueTag, params CueNotifyDefinition[] newNotifies)
        {
            cueTag = newCueTag;
            notifies = newNotifies ?? Array.Empty<CueNotifyDefinition>();
        }
    }
}
