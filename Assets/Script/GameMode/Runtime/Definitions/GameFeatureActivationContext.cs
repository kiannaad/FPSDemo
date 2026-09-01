using System;

namespace CGame
{
    public readonly struct GameFeatureActivationContext
    {
        public GameFeatureActivationContext(IGameFeatureActivationHost host, Guid ownerId, string featureId)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            OwnerId = ownerId;
            FeatureId = featureId;
        }

        public IGameFeatureActivationHost Host { get; }
        public World World => Host.World;
        public Guid OwnerId { get; }
        public string FeatureId { get; }

        public GameFeatureActivationReceipt InstallComponent(object component) =>
            Host.InstallComponent(component, OwnerId);
    }
}
