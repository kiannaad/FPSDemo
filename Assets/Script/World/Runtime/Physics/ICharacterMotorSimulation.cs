using System;

namespace CGame
{
    public interface ICharacterMotorSimulation : IDisposable
    {
        void Step(float deltaTime);

        void ConsumePostPhysicsEvents(float deltaTime);

        void Present(float currentTime);
    }
}
