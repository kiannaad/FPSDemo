using UnityEngine;

namespace CGame.Ability.Cues
{
    public abstract class CueNotifyDefinition : ScriptableObject
    {
        public abstract void HandleCue(GameplayCueEventType eventType, GameplayCueParameters parameters, GameplayCueHandle handle);
    }
}
