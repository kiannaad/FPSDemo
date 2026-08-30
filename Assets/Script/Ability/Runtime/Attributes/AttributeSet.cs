namespace CGame.Ability.Attributes
{
    public abstract class AttributeSet
    {
        protected internal virtual void PostGameplayEffectExecute(
            GameplayAttribute attribute,
            CGame.Ability.Effects.GameplayEffectContext context,
            CGame.Ability.Effects.IGameplayEffectExecution execution)
        {
        }
    }
}
