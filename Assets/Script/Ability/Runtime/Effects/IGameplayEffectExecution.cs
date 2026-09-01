using System;
using CGame.Ability.Attributes;

namespace CGame.Ability.Effects
{
    public interface IGameplayEffectExecution
    {
        void SetCurrentValue(GameplayAttributeData data, float value);
        void QueueEvent(Action callback);
    }
}
