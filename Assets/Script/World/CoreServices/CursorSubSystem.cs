using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame
{
    public sealed class CursorSubSystem : PlayerSubSystem
    {
        private CursorLockMode previousLockMode;
        private bool previousVisible;
        private bool ownsState;

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        protected override void OnBeginPlay()
        {
            previousLockMode = Cursor.lockState;
            previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ownsState = true;
        }

        protected override void OnEndPlay()
        {
            Restore();
        }

        protected override Task OnShutdownAsync()
        {
            Restore();
            return Task.CompletedTask;
        }

        private void Restore()
        {
            if (!ownsState)
            {
                return;
            }

            Cursor.lockState = previousLockMode;
            Cursor.visible = previousVisible;
            ownsState = false;
        }
    }
}
