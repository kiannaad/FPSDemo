using UnityEngine;

namespace CGame
{
    public sealed class GameplayRecoilState
    {
        private float recoveryDegreesPerSecond;

        public Vector2 Offset { get; private set; }

        public void ApplyKick(Vector2 kick, float recoverySpeed)
        {
            Offset += kick;
            recoveryDegreesPerSecond = Mathf.Max(0f, recoverySpeed);
        }

        public Vector2 Advance(float deltaTime)
        {
            if (Offset == Vector2.zero || deltaTime <= 0f || recoveryDegreesPerSecond <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 previous = Offset;
            Offset = Vector2.MoveTowards(Offset, Vector2.zero, recoveryDegreesPerSecond * deltaTime);
            return Offset - previous;
        }

        public Vector2 Clear()
        {
            Vector2 delta = -Offset;
            Offset = Vector2.zero;
            recoveryDegreesPerSecond = 0f;
            return delta;
        }
    }
}
