using UnityEngine;

namespace CGame.Animation
{
    public static class KCurves
    {
        public static bool IsWeightRelevant(float weight)
        {
            return !Mathf.Approximately(weight, 0f);
        }
    }
}
