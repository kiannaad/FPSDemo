using CGame.Ability;

namespace CGame.InventoryEquipment
{
    public sealed class RepeatFireTask : AbilityTask
    {
        private readonly WeaponInstance weapon;
        private readonly float fireInterval;
        private float elapsed;

        public RepeatFireTask(WeaponInstance weapon, float fireInterval)
        {
            this.weapon = weapon ?? throw new System.ArgumentNullException(nameof(weapon));
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
            if (weapon.TryFire().Succeeded)
            {
                FiredShotCount++;
            }
        }
    }
}
