namespace CGame.Ability.Attributes
{
    public sealed class CombatSet : AttributeSet
    {
        public static GameplayAttribute BaseDamageAttribute { get; } =
            GameplayAttribute.Create<CombatSet>(nameof(BaseDamage), set => set.BaseDamage);

        public CombatSet(float baseDamage = 0f)
        {
            BaseDamage = new GameplayAttributeData(baseDamage);
        }

        public GameplayAttributeData BaseDamage { get; }
    }
}
