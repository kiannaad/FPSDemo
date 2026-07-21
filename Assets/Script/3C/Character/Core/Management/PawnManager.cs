using System.Collections.Generic;

namespace CGame
{
    public class PawnManager : IManager
    {
        private readonly List<Pawn> pawns = new List<Pawn>();
        private readonly Dictionary<Pawn, PawnRegistration> registrations = new Dictionary<Pawn, PawnRegistration>();

        public override int Priority => 80;

        /// <summary>
        /// 注册 Pawn 实例。
        /// </summary>
        public void RegisteringPawn(Pawn pawn)
        {
            RegisterPawn(pawn);
        }

        public IPawnRegistration RegisterPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            if (registrations.TryGetValue(pawn, out PawnRegistration existing))
            {
                return existing;
            }

            pawns.Add(pawn);
            var registration = new PawnRegistration(this, pawn);
            registrations.Add(pawn, registration);
            return registration;
        }

        /// <summary>
        /// 注销 Pawn 实例。
        /// </summary>
        public void UnregisteringPawn(Pawn pawn)
        {
            if (pawn == null || !registrations.TryGetValue(pawn, out PawnRegistration registration))
            {
                return;
            }

            registration.Dispose();
        }

        /// <summary>
        /// 更新所有 Pawn 的固定帧逻辑。
        /// </summary>
        public override void FixedUpdate(float elapseSeconds)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                pawns[i].FixedUpdatingPawn(elapseSeconds);
            }
        }

        /// <summary>
        /// 更新所有 Pawn 的普通帧逻辑。
        /// </summary>
        public override void Update(float elapseSeconds)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                pawns[i].UpdatingPawn(elapseSeconds);
            }
        }

        /// <summary>
        /// 更新所有 Pawn 的渲染后逻辑。
        /// </summary>
        public override void LateUpdate(float elapseSeconds)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                pawns[i].LateUpdatingPawn(elapseSeconds);
            }
        }

        /// <summary>
        /// 关闭 Pawn 管理器并清理所有 Pawn。
        /// </summary>
        public override void Shutdown()
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                pawns[i].ShuttingDownPawn();
            }

            pawns.Clear();
            foreach (PawnRegistration registration in registrations.Values)
            {
                registration.Invalidate();
            }

            registrations.Clear();
        }

        private void Release(PawnRegistration registration, Pawn pawn)
        {
            if (!registrations.TryGetValue(pawn, out PawnRegistration current) || current != registration)
            {
                return;
            }

            registrations.Remove(pawn);
            if (pawns.Remove(pawn))
            {
                pawn.ShuttingDownPawn();
            }
        }

        private sealed class PawnRegistration : IPawnRegistration
        {
            private PawnManager owner;
            private Pawn pawn;

            public PawnRegistration(PawnManager owner, Pawn pawn)
            {
                this.owner = owner;
                this.pawn = pawn;
            }

            public bool IsActive => owner != null && pawn != null;

            public void Dispose()
            {
                PawnManager currentOwner = owner;
                Pawn currentPawn = pawn;
                owner = null;
                pawn = null;
                currentOwner?.Release(this, currentPawn);
            }

            public void Invalidate()
            {
                owner = null;
                pawn = null;
            }
        }
    }
}
