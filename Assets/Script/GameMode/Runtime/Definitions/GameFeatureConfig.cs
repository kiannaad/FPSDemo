using System;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "GameFeatureConfig", menuName = "CGame/Gameplay/Game Feature Config")]
    public sealed class GameFeatureConfig : ScriptableObject
    {
        [SerializeField] private string featureId;
        [SerializeField] private string[] dependencies = Array.Empty<string>();
        [SerializeField] private string[] requiredAssetLocations = Array.Empty<string>();
        [SerializeField] private GameFeatureAction[] actions = Array.Empty<GameFeatureAction>();

        public string FeatureId => featureId;
        public System.Collections.Generic.IReadOnlyList<string> Dependencies => dependencies;
        public System.Collections.Generic.IReadOnlyList<string> RequiredAssetLocations => requiredAssetLocations;
        public System.Collections.Generic.IReadOnlyList<GameFeatureAction> Actions => actions;

        public void Configure(string id)
        {
            featureId = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("Feature ID is required.", nameof(id))
                : id.Trim();
        }

        public void Configure(string id, string[] featureDependencies, string[] assetLocations, params GameFeatureAction[] featureActions)
        {
            Configure(id);
            dependencies = featureDependencies == null ? Array.Empty<string>() : (string[])featureDependencies.Clone();
            requiredAssetLocations = assetLocations == null ? Array.Empty<string>() : (string[])assetLocations.Clone();
            actions = featureActions == null ? Array.Empty<GameFeatureAction>() : (GameFeatureAction[])featureActions.Clone();
            if (Array.Exists(dependencies, string.IsNullOrWhiteSpace)) throw new ArgumentException("Feature dependencies cannot be empty.", nameof(featureDependencies));
            if (Array.Exists(actions, action => action == null)) throw new ArgumentException("Feature actions cannot contain null.", nameof(featureActions));
        }
    }
}
