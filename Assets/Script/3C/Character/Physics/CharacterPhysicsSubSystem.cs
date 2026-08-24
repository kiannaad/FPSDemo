using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame
{
    public sealed class CharacterPhysicsSubSystem : WorldSubSystem
    {
        private readonly CharacterPhysicsSettings configuredSettings;
        private CharacterPhysicsWorld physicsWorld;

        public CharacterPhysicsSubSystem(CharacterPhysicsSettings settings = null)
        {
            configuredSettings = settings;
        }

        public CharacterPhysicsWorld PhysicsWorld => physicsWorld;

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
            physicsWorld?.Step(deltaTime, Time.time);
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
