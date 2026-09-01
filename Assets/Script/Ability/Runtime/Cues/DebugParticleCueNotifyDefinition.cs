using System.Collections.Generic;
using UnityEngine;

namespace CGame.Ability.Cues
{
    [CreateAssetMenu(fileName = "DebugParticleCueNotify", menuName = "CGame/Ability/Debug Particle Cue Notify")]
    public sealed class DebugParticleCueNotifyDefinition : CueNotifyDefinition
    {
        [SerializeField] private ParticleSystem particlePrefab;
        [SerializeField] private float lifetimeSeconds = 0.5f;
        private readonly Dictionary<long, ParticleSystem> activeParticles = new Dictionary<long, ParticleSystem>();

        public override void HandleCue(GameplayCueEventType eventType, GameplayCueParameters parameters, GameplayCueHandle handle)
        {
            if (eventType == GameplayCueEventType.Removed)
            {
                if (activeParticles.TryGetValue(handle.Value, out ParticleSystem activeParticle))
                {
                    activeParticles.Remove(handle.Value);
                    Destroy(activeParticle.gameObject);
                }

                return;
            }

            if (eventType != GameplayCueEventType.Executed && eventType != GameplayCueEventType.OnActive)
            {
                return;
            }

            Vector3 location = parameters.HasLocation ? parameters.Location : Vector3.zero;
            ParticleSystem particle = particlePrefab == null
                ? CreateFallbackParticle(location)
                : Instantiate(particlePrefab, location, Quaternion.identity);
            particle.Play();
            if (handle.IsValid)
            {
                activeParticles[handle.Value] = particle;
            }
            else
            {
                Destroy(particle.gameObject, lifetimeSeconds);
            }
        }

        private static ParticleSystem CreateFallbackParticle(Vector3 location)
        {
            var instance = new GameObject("GameplayCueDebugParticle");
            instance.transform.position = location;
            ParticleSystem particle = instance.AddComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particle.main;
            main.duration = 0.2f;
            main.startLifetime = 0.2f;
            main.startSpeed = 0f;
            main.startSize = 0.12f;
            main.loop = false;
            return particle;
        }
    }
}
