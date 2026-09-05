using System;
using System.Collections.Generic;
using CGame.Network;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace CGame.Editor
{
    public static class EnemyPresentationClosureValidator
    {
        [MenuItem("CGame/Enemy/Validate TPS Bundle Presentation Closure")]
        public static void ValidateCatalogAsset()
        {
            EnemyPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(
                "Assets/Settings/Gameplay/Enemy/EnemyPresentationCatalog.asset");
            Validate(catalog);
            Debug.Log("[EnemyPresentation] TPS Bundle presentation closure validated.");
        }

        public static void Validate(EnemyPresentationCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemyArchetypeSpec archetype in catalog.Archetypes)
            {
                if (archetype == null) throw new InvalidOperationException("Enemy presentation catalog contains a missing archetype.");
                if (!ids.Add(archetype.ArchetypeId)) throw new InvalidOperationException($"Duplicate enemy archetype ID: {archetype.ArchetypeId}.");
            }

            foreach (EnemyArchetypeSpec archetype in catalog.Archetypes)
            {
                ValidatePrefab(archetype.PresentationPrefab, archetype.ArchetypeId);
            }
        }

        public static void ValidatePrefab(GameObject prefab, string archetypeId)
        {
            if (prefab == null) throw new InvalidOperationException($"Enemy archetype {archetypeId} has no presentation prefab.");
            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException($"Enemy archetype {archetypeId} prefab is not an asset.");
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
            {
                if (dependency.StartsWith("Assets/TPS Bundle/", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Enemy archetype {archetypeId} depends on TPS Bundle asset: {dependency}.");
            }

            if (prefab.GetComponentInChildren<EnemyPresentation>(true) == null)
                throw new InvalidOperationException($"Enemy archetype {archetypeId} has no EnemyPresentation component.");
            if (prefab.GetComponentInChildren<NavMeshAgent>(true) != null)
                throw new InvalidOperationException($"Enemy archetype {archetypeId} contains NavMeshAgent.");
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator?.applyRootMotion == true)
                throw new InvalidOperationException($"Enemy archetype {archetypeId} enables Animator Root Motion.");
            if (!(animator?.runtimeAnimatorController is AnimatorController controller) ||
                controller.layers.Length == 0 || controller.layers[0].stateMachine.defaultState?.motion == null)
                throw new InvalidOperationException($"Enemy archetype {archetypeId} has no playable default Animator state.");

            foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) throw new InvalidOperationException($"Enemy archetype {archetypeId} contains a missing script.");
                if (behaviour is EnemyPresentation) continue;
                string typeName = behaviour.GetType().Name;
                throw new InvalidOperationException($"Enemy archetype {archetypeId} contains forbidden behaviour: {typeName}.");
            }
        }
    }
}
