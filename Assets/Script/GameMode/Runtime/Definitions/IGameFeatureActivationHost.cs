using System;

namespace CGame
{
    public interface IGameFeatureActivationHost
    {
        World World { get; }
        GameFeatureActivationReceipt InstallComponent(object component, Guid ownerId);
    }
}
