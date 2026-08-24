using System;
using CGame.Ability;

namespace CGame.InventoryEquipment
{
    public sealed class RepeatFireTask : AbilityTask
    {
        private readonly Func<FireResult> fire;
        private readonly Action<FireResult> onFireFailed;
        private readonly float fireInterval;
        private float elapsed;

        public RepeatFireTask(Func<FireResult> fire, Action<FireResult> onFireFailed, float fireInterval)
        {
            this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
            this.onFireFailed = onFireFailed ?? throw new ArgumentNullException(nameof(onFireFailed));
            if (fireInterval <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(fireInterval));
            }

            this.fireInterval = fireInterval;
        }

        public int FiredShotCount { get; private set; }

        protected override void OnTick(float deltaTime)
        {
            elapsed += deltaTime;
            if (elapsed < fireInterval)
            {
                return;
            }

            elapsed -= fireInterval;
            // One task tick can produce at most one shot. This keeps the fire
            // timeline deterministic when a frame arrives late.
            FireResult result = fire();
            if (result.Succeeded)
            {
                FiredShotCount++;
            }
            else
            {
                onFireFailed(result);
            }
        }
    }
}
