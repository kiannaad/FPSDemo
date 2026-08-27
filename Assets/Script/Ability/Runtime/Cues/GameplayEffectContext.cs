namespace CGame.Ability.Cues
{
    public sealed class GameplayEffectContext
    {
        public GameplayEffectContext(object instigator, object effectCauser, object sourceObject, GameplayHitResult? hitResult = null)
        {
            Instigator = instigator;
            EffectCauser = effectCauser;
            SourceObject = sourceObject;
            HitResult = hitResult;
        }

        public object Instigator { get; }
        public object EffectCauser { get; }
        public object SourceObject { get; }
        public GameplayHitResult? HitResult { get; }
    }
}
