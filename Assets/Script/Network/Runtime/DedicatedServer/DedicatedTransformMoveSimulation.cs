using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedTransformMoveSimulation : IAuthorityMoveSimulation
    {
        private const float FixedDeltaTime = 1f / 60f;
        private const float MoveSpeed = 5f;
        private readonly Transform transform;

        public DedicatedTransformMoveSimulation(Transform transform)
        {
            this.transform = transform;
        }

        public AuthorityState Simulate(PawnMove move, long serverTick)
        {
            Vector2 input = move.MovementInput.ToVector2();
            Quaternion yaw = Quaternion.Euler(0f, move.View.YawDegrees, 0f);
            Vector3 direction = yaw * new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            transform.position += direction * (MoveSpeed * FixedDeltaTime);
            transform.rotation = yaw;
            Debug.Log($"[DedicatedServer][038] AuthorityMoveSimulated PawnId={move.PawnId} Sequence={move.Sequence} ServerTick={serverTick}");
            return Capture(serverTick);
        }

        public AuthorityState Capture(long serverTick) =>
            new AuthorityState(serverTick, QuantizedVector3.FromMeters(transform.position));
    }
}
