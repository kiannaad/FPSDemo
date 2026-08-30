using System;
using System.Collections.Generic;
using CGame.Ability.Attributes;

namespace CGame.Ability.Effects
{
    internal sealed class GameplayEffectExecutionPlan : IGameplayEffectExecution
    {
        private readonly Entry[] entries;
        private readonly GameplayEffectContext context;
        private readonly Dictionary<GameplayAttributeData, float> originalValues =
            new Dictionary<GameplayAttributeData, float>();
        private readonly List<Action> bufferedEvents = new List<Action>();

        private GameplayEffectExecutionPlan(Entry[] entries, GameplayEffectContext context)
        {
            this.entries = entries;
            this.context = context;
        }

        public static bool TryBuild(
            IReadOnlyDictionary<Type, AttributeSet> attributeSets,
            IReadOnlyList<EvaluatedGameplayEffectModifier> modifiers,
            GameplayEffectContext context,
            out GameplayEffectExecutionPlan plan,
            out GameplayEffectFailureReason failureReason)
        {
            var entries = new Entry[modifiers.Count];
            var projectedValues = new Dictionary<GameplayAttributeData, float>();
            for (int index = 0; index < modifiers.Count; index++)
            {
                EvaluatedGameplayEffectModifier modifier = modifiers[index];
                if (!attributeSets.TryGetValue(modifier.TargetAttribute.SetType, out AttributeSet attributeSet))
                {
                    plan = null;
                    failureReason = GameplayEffectFailureReason.MissingTargetAttributeSet;
                    return false;
                }

                GameplayAttributeData data = modifier.TargetAttribute.GetData(attributeSet);
                float currentValue = projectedValues.TryGetValue(data, out float projectedValue)
                    ? projectedValue
                    : data.CurrentValue;
                float value = modifier.Operation == GameplayEffectModifierOperation.Add
                    ? currentValue + modifier.Magnitude
                    : modifier.Magnitude;
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    plan = null;
                    failureReason = GameplayEffectFailureReason.InvalidMagnitude;
                    return false;
                }

                projectedValues[data] = value;
                entries[index] = new Entry(attributeSet, modifier.TargetAttribute, data, value);
            }

            plan = new GameplayEffectExecutionPlan(entries, context);
            failureReason = GameplayEffectFailureReason.None;
            return true;
        }

        public bool Execute(Action onCommitted = null)
        {
            try
            {
                foreach (Entry entry in entries)
                {
                    SetCurrentValue(entry.Data, entry.Value);
                    entry.AttributeSet.PostGameplayEffectExecute(entry.Attribute, context, this);
                }

                try
                {
                    onCommitted?.Invoke();
                }
                catch
                {
                    // GameplayCue presentation failure must not roll back committed gameplay attributes.
                }

                foreach (Action bufferedEvent in bufferedEvents)
                {
                    try
                    {
                        bufferedEvent();
                    }
                    catch
                    {
                    }
                }

                return true;
            }
            catch
            {
                foreach (KeyValuePair<GameplayAttributeData, float> originalValue in originalValues)
                {
                    originalValue.Key.SetCurrentValue(originalValue.Value);
                }

                return false;
            }
        }

        public void SetCurrentValue(GameplayAttributeData data, float value)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!originalValues.ContainsKey(data))
            {
                originalValues.Add(data, data.CurrentValue);
            }

            data.SetCurrentValue(value);
        }

        public void QueueEvent(Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            bufferedEvents.Add(callback);
        }

        private readonly struct Entry
        {
            public Entry(
                AttributeSet attributeSet,
                GameplayAttribute attribute,
                GameplayAttributeData data,
                float value)
            {
                AttributeSet = attributeSet;
                Attribute = attribute;
                Data = data;
                Value = value;
            }

            public AttributeSet AttributeSet { get; }
            public GameplayAttribute Attribute { get; }
            public GameplayAttributeData Data { get; }
            public float Value { get; }
        }
    }
}
