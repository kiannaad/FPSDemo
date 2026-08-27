using System;
using UnityEngine;

namespace CGame
{
    public sealed class PawnShotQueryComponent : ActorComponent
    {
        private PendingShot pendingShot;

        public bool HasPendingShot => pendingShot != null;

        public bool TryQueueShot(
            Func<Vector3, Vector3, FireResult> execute,
            Action<FireResult> completed,
            out FireResult queueResult)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            if (pendingShot != null)
            {
                queueResult = FireResult.Queued(0);
                return false;
            }

            pendingShot = new PendingShot(execute, completed);
            queueResult = FireResult.Queued(0);
            return true;
        }

        protected override void OnInitialize()
        {
            AddTickTask("Pawn.ShotQuery", TickGroup.TG_LatePresentation, Tick);
        }

        protected override void OnEndPlay()
        {
            CancelPendingShot("Pawn EndPlay cancelled the queued shot.");
        }

        protected override void OnShutdown()
        {
            CancelPendingShot("Pawn shutdown cancelled the queued shot.");
        }

        private void Tick(float deltaTime)
        {
            if (pendingShot == null)
            {
                return;
            }

            PendingShot shot = pendingShot;
            pendingShot = null;
            if (!(Owner is Pawn pawn) || !pawn.TryGetCurrentCameraShotRay(out Vector3 origin, out Vector3 direction))
            {
                Complete(shot, FireResult.Failed("Camera did not publish a current-frame shot ray.", 0));
                return;
            }

            try
            {
                Complete(shot, shot.Execute(origin, direction));
            }
            catch (Exception exception)
            {
                Complete(shot, FireResult.Failed($"Queued shot execution threw {exception.GetType().Name}.", 0));
            }
        }

        private void CancelPendingShot(string reason)
        {
            if (pendingShot == null)
            {
                return;
            }

            PendingShot shot = pendingShot;
            pendingShot = null;
            Complete(shot, FireResult.Failed(reason, 0));
        }

        private static void Complete(PendingShot shot, FireResult result)
        {
            shot.Completed?.Invoke(result);
        }

        private sealed class PendingShot
        {
            public PendingShot(Func<Vector3, Vector3, FireResult> execute, Action<FireResult> completed)
            {
                Execute = execute;
                Completed = completed;
            }

            public Func<Vector3, Vector3, FireResult> Execute { get; }

            public Action<FireResult> Completed { get; }
        }
    }
}
