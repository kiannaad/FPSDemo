using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class ExperienceActivationTransaction : IDisposable
    {
        private readonly IGameFeatureActivationHost owner;
        private readonly Guid ownerId = Guid.NewGuid();
        private readonly List<GameFeatureActivationReceipt> receipts = new List<GameFeatureActivationReceipt>();
        private bool activationAttempted;

        public ExperienceActivationTransaction(IGameFeatureActivationHost activationOwner)
        {
            owner = activationOwner ?? throw new ArgumentNullException(nameof(activationOwner));
        }

        public Guid OwnerId => ownerId;

        public IReadOnlyList<GameFeatureConfig> BuildActivationOrder(ExperienceDefinition experience)
        {
            if (experience == null) throw new ArgumentNullException(nameof(experience));
            experience.ValidateConfiguration();
            var byId = new Dictionary<string, GameFeatureConfig>(StringComparer.Ordinal);
            foreach (GameFeatureConfig feature in experience.GameFeatures) byId.Add(feature.FeatureId, feature);
            var result = new List<GameFeatureConfig>();
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameFeatureConfig feature in experience.GameFeatures) Visit(feature, byId, visiting, visited, result);
            return result;
        }

        public void Activate(ExperienceDefinition experience)
        {
            if (activationAttempted) throw new InvalidOperationException("Experience transaction can only activate once.");
            activationAttempted = true;
            IReadOnlyList<GameFeatureConfig> order = BuildActivationOrder(experience);
            try
            {
                foreach (GameFeatureConfig feature in order)
                {
                    foreach (GameFeatureAction action in feature.Actions)
                    {
                        GameFeatureActivationReceipt receipt = action.Activate(new GameFeatureActivationContext(owner, ownerId, feature.FeatureId));
                        if (receipt == null) throw new InvalidOperationException($"Feature {feature.FeatureId} returned a null receipt.");
                        if (receipt.OwnerId != ownerId)
                        {
                            receipt.Dispose();
                            throw new InvalidOperationException($"Feature {feature.FeatureId} returned an invalid OwnerId receipt.");
                        }
                        receipts.Add(receipt);
                    }
                }
            }
            catch { Rollback(); throw; }
        }

        public void Dispose() => Rollback();

        private void Rollback()
        {
            for (int index = receipts.Count - 1; index >= 0; index--) receipts[index].Dispose();
            receipts.Clear();
        }

        private static void Visit(GameFeatureConfig feature, Dictionary<string, GameFeatureConfig> byId, HashSet<string> visiting, HashSet<string> visited, List<GameFeatureConfig> result)
        {
            if (visited.Contains(feature.FeatureId)) return;
            if (!visiting.Add(feature.FeatureId)) throw new InvalidOperationException($"GameFeature dependency cycle includes {feature.FeatureId}.");
            foreach (string dependencyId in feature.Dependencies)
            {
                if (!byId.TryGetValue(dependencyId, out GameFeatureConfig dependency)) throw new InvalidOperationException($"GameFeature {feature.FeatureId} requires missing {dependencyId}.");
                Visit(dependency, byId, visiting, visited, result);
            }
            visiting.Remove(feature.FeatureId);
            visited.Add(feature.FeatureId);
            result.Add(feature);
        }
    }
}
