using CGame.Ability.Cues;
using UnityEngine;

namespace CGame.Ability.Effects
{
    public sealed class GameplayEffectContext
    {
        public GameplayEffectContext(object instigator, object effectCauser, object sourceObject)
            : this(instigator, effectCauser, sourceObject, null, null, default, false)
        {
        }

        public GameplayEffectContext(
            object instigator,
            object effectCauser,
            object sourceObject,
            GameplayHitResult? hitResult)
            : this(instigator, effectCauser, sourceObject, null, hitResult, default, false)
        {
        }

        public GameplayEffectContext(
            object instigator,
            object effectCauser,
            object sourceObject,
            object ability,
            GameplayHitResult? hitResult,
            Vector3 origin,
            bool hasOrigin = true)
        {
            Instigator = instigator;
            EffectCauser = effectCauser;
            SourceObject = sourceObject;
            Ability = ability;
            HitResult = hitResult;
            Origin = origin;
            HasOrigin = hasOrigin;
        }

        public object Instigator { get; }
        public object EffectCauser { get; }
        public object SourceObject { get; }
        public object Ability { get; }
        public GameplayHitResult? HitResult { get; }
        public Vector3 Origin { get; }
        public bool HasOrigin { get; }
    }
}
