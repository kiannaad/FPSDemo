namespace CGame.Ability
{
    public interface IAbilityExecutionGate
    {
        bool CanExecuteLocally(AbilitySystemComponent abilitySystem);
    }
}
