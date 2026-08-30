using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "ExperienceDefinition", menuName = "CGame/Gameplay/Experience Definition")]
    public sealed class ExperienceDefinition : ScriptableObject
    {
        [SerializeField] private GameFeatureConfig[] gameFeatures = Array.Empty<GameFeatureConfig>();

        public IReadOnlyList<GameFeatureConfig> GameFeatures => gameFeatures;

        public void Configure(params GameFeatureConfig[] features)
        {
            gameFeatures = features == null ? Array.Empty<GameFeatureConfig>() : (GameFeatureConfig[])features.Clone();
            ValidateConfiguration();
        }

        public void ValidateConfiguration()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameFeatureConfig feature in gameFeatures)
            {
                if (feature == null)
                {
                    throw new InvalidOperationException("ExperienceDefinition cannot contain a null GameFeatureConfig.");
                }

                if (string.IsNullOrWhiteSpace(feature.FeatureId) || !ids.Add(feature.FeatureId))
                {
                    throw new InvalidOperationException("ExperienceDefinition requires unique, non-empty Feature IDs.");
                }
            }
        }
    }
}
