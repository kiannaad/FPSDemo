using System.Threading;
using System.Threading.Tasks;
using System;
using UnityEngine;

namespace CGame
{
    public sealed class CharacterPhysicsSubSystem : WorldSubSystem
    {
        private readonly CharacterPhysicsSettings configuredSettings;
        private readonly FixedStepAccumulator stepAccumulator = new FixedStepAccumulator(FixedStepSeconds);
        private CharacterPhysicsWorld physicsWorld;
        private float simulationTime;

        public CharacterPhysicsSubSystem(CharacterPhysicsSettings settings = null)
        {
            configuredSettings = settings;
        }

        public CharacterPhysicsWorld PhysicsWorld => physicsWorld;
        public long FixedStepCount { get; private set; }
        public event Action<long> FixedStepCompleted;
        public const float FixedStepSeconds = 1f / 60f;

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CharacterPhysicsSettings settings = configuredSettings;
            bool ownsSettings = settings == null;
            if (ownsSettings)
            {
                settings = ScriptableObject.CreateInstance<CharacterPhysicsSettings>();
                settings.name = "RuntimeCharacterPhysicsSettings";
            }

            // Character physics is stepped once per rendered frame from
            // GameInstance.Update. Fixed-step render interpolation would restore
            // the previous transform before Camera reads the current frame.
            settings.Interpolate = false;

            physicsWorld = new CharacterPhysicsWorld(settings, ownsSettings);
            simulationTime = Time.time;
            AddTickTask("CharacterPhysics.Step", TickGroup.TG_PrePhysics, Step);
            AddTickTask("CharacterPhysics.Events", TickGroup.TG_PostPhysics, ConsumeEvents);
            AddTickTask("CharacterPhysics.Present", TickGroup.TG_PhysicsMovement, Present);
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync()
        {
            physicsWorld?.Dispose();
            physicsWorld = null;
            return Task.CompletedTask;
        }

        private void Step(float deltaTime)
        {
            if (physicsWorld == null) return;
            int stepCount = stepAccumulator.Consume(deltaTime);
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                simulationTime += FixedStepSeconds;
                physicsWorld.Step(FixedStepSeconds, simulationTime);
                FixedStepCount++;
                FixedStepCompleted?.Invoke(FixedStepCount);
            }
        }

        private void ConsumeEvents(float deltaTime)
        {
            physicsWorld?.ConsumePostPhysicsEvents(deltaTime);
        }

        private void Present(float deltaTime)
        {
            physicsWorld?.Present(Time.time);
        }
    }
}
