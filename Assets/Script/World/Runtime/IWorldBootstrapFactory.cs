using System.Collections.Generic;

namespace CGame
{
    public interface IWorldBootstrapFactory
    {
        IReadOnlyList<IWorldCoreService> CreateCoreServices();

        GameLauncher CreateLauncher();

        ICharacterMotorSimulation CreateCharacterMotorSimulation();
    }
}
