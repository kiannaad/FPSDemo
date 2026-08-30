using System;

namespace CGame.Ability.Attributes
{
    public sealed class GameplayAttributeData
    {
        public GameplayAttributeData(float initialValue = 0f)
        {
            SetBaseAndCurrentValue(initialValue);
        }

        public float BaseValue { get; private set; }
        public float CurrentValue { get; private set; }

        internal void SetBaseAndCurrentValue(float value)
        {
            ValidateFinite(value, nameof(value));
            BaseValue = value;
            CurrentValue = value;
        }

        internal void SetCurrentValue(float value)
        {
            ValidateFinite(value, nameof(value));
            CurrentValue = value;
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Gameplay attribute values must be finite.");
            }
        }
    }
}
