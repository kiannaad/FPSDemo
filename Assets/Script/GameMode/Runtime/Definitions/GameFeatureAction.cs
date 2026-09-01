using UnityEngine;

namespace CGame
{
    public abstract class GameFeatureAction : ScriptableObject
    {
        public abstract GameFeatureActivationReceipt Activate(GameFeatureActivationContext context);
    }
}
