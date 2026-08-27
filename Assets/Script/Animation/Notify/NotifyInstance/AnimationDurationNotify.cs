using System;

namespace CGame.Animation
{
    [Serializable]
    public class AnimationDurationNotify : AnimationNotify
    {
        public virtual void OnBegin(Pawn pawn)
        {
        }

        public virtual void OnTick(Pawn pawn)
        {
        }

        public virtual void OnEnd(Pawn pawn, AnimationNotifyEndReason reason)
        {
        }
    }
}
