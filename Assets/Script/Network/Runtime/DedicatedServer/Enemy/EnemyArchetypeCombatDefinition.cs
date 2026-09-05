using System;
using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "EnemyArchetypeCombatDefinition", menuName = "CGame/Network/Enemy Archetype Combat Definition")]
    public sealed class EnemyArchetypeCombatDefinition : ScriptableObject
    {
        [SerializeField] private string archetypeId;
        [SerializeField] private PatrolRouteDefinition patrolRoute;
        [SerializeField] private EnemyFireDefinition fireDefinition = EnemyFireDefinition.Default;

        public string ArchetypeId => archetypeId;
        public PatrolRouteDefinition PatrolRoute => patrolRoute;
        public EnemyFireDefinition FireDefinition => fireDefinition.IsValid
            ? fireDefinition
            : EnemyFireDefinition.Default;

        public void Configure(string archetypeId, PatrolRouteDefinition patrolRoute)
        {
            Configure(archetypeId, patrolRoute, EnemyFireDefinition.Default);
        }

        public void Configure(string archetypeId, PatrolRouteDefinition patrolRoute, EnemyFireDefinition fireDefinition)
        {
            ValidateValues(archetypeId, patrolRoute, fireDefinition);
            this.archetypeId = archetypeId;
            this.patrolRoute = patrolRoute;
            this.fireDefinition = fireDefinition;
        }

        public void Validate()
        {
            ValidateValues(archetypeId, patrolRoute, FireDefinition);
        }

        private static void ValidateValues(string archetypeId, PatrolRouteDefinition patrolRoute, EnemyFireDefinition fireDefinition)
        {
            if (string.IsNullOrWhiteSpace(archetypeId))
                throw new InvalidOperationException("Enemy archetype combat definition requires an ArchetypeId.");
            if (patrolRoute == null)
                throw new InvalidOperationException("Enemy archetype combat definition requires a PatrolRoute.");
            fireDefinition.Validate();
        }
    }
}
