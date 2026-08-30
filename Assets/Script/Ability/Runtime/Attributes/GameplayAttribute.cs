using System;

namespace CGame.Ability.Attributes
{
    public sealed class GameplayAttribute
    {
        private readonly Func<AttributeSet, GameplayAttributeData> dataAccessor;

        private GameplayAttribute(Type setType, string name, Func<AttributeSet, GameplayAttributeData> dataAccessor)
        {
            SetType = setType ?? throw new ArgumentNullException(nameof(setType));
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("An attribute name is required.", nameof(name)) : name;
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
        }

        public Type SetType { get; }
        public string Name { get; }

        public GameplayAttributeData GetData(AttributeSet attributeSet)
        {
            if (attributeSet == null)
            {
                throw new ArgumentNullException(nameof(attributeSet));
            }

            if (attributeSet.GetType() != SetType)
            {
                throw new ArgumentException($"Attribute {Name} belongs to {SetType.FullName}, not {attributeSet.GetType().FullName}.", nameof(attributeSet));
            }

            return dataAccessor(attributeSet);
        }

        public static GameplayAttribute Create<TSet>(string name, Func<TSet, GameplayAttributeData> dataAccessor)
            where TSet : AttributeSet
        {
            if (dataAccessor == null)
            {
                throw new ArgumentNullException(nameof(dataAccessor));
            }

            return new GameplayAttribute(typeof(TSet), name, set => dataAccessor((TSet)set));
        }
    }
}
