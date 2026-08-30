using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame
{
    public class EnemyTargetPawnFactory
    {
        public virtual Task<Pawn> CreateAsync(EnemyPlayerStateDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            GameObject root = null;
            try
            {
                root = UnityEngine.Object.Instantiate(definition.PawnPrefab, position, rotation);
                root.name = "TargetEnemy";
                root.SetActive(false);
                CharacterPhysicsMotor motor = root.GetComponent<CharacterPhysicsMotor>()
                    ?? throw new InvalidOperationException("Target prefab requires CharacterPhysicsMotor on its root.");
                Animator[] animators = root.GetComponentsInChildren<Animator>(true);
                if (animators.Length != 1 || animators[0].runtimeAnimatorController == null)
                    throw new InvalidOperationException("Target prefab requires exactly one Animator with an Idle controller.");
                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                for (int index = 0; index < cameras.Length; index++) cameras[index].gameObject.SetActive(false);
                var components = new List<ActorComponent>
                {
                    new PawnMovementComponent(motor),
                    new HealthDeathComponent(definition.MaximumHealth)
                };
                return Task.FromResult(new Pawn(root, components));
            }
            catch
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }
    }
}
