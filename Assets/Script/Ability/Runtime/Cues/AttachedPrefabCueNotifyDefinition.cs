using System;
using System.Linq;
using UnityEngine;

namespace CGame.Ability.Cues
{
    [CreateAssetMenu(fileName = "AttachedPrefabCueNotify", menuName = "CGame/Ability/Attached Prefab Cue Notify")]
    public sealed class AttachedPrefabCueNotifyDefinition : CueNotifyDefinition
    {
        [SerializeField] private GameObject effectPrefab;
        [SerializeField] private string attachmentName;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private float lifetimeSeconds = 0.1f;

        public void Configure(GameObject prefab, string attachment, Vector3 position, Vector3 eulerAngles, float lifetime)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (float.IsNaN(lifetime) || float.IsInfinity(lifetime) || lifetime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            effectPrefab = prefab;
            attachmentName = attachment;
            localPosition = position;
            localEulerAngles = eulerAngles;
            lifetimeSeconds = lifetime;
        }

        public override void HandleCue(GameplayCueEventType eventType, GameplayCueParameters parameters, GameplayCueHandle handle)
        {
            if (eventType != GameplayCueEventType.Executed || effectPrefab == null) return;
            var target = parameters.Target as GameObject;
            if (target == null) return;
            Transform attachment = string.IsNullOrEmpty(attachmentName) ? target.transform :
                target.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == attachmentName);
            if (attachment == null)
            {
                Debug.LogWarning($"GameplayCue '{name}' cannot find attachment '{attachmentName}' on '{target.name}'.");
                return;
            }

            // Effects belong to the rendered attachment, never to authority motion.
            GameObject effect = Instantiate(effectPrefab, attachment, false);
            effect.transform.localPosition = localPosition;
            effect.transform.localRotation = Quaternion.Euler(localEulerAngles);
            ParticleSystem particle = effect.GetComponent<ParticleSystem>();
            if (particle != null) particle.Play(true);
            Destroy(effect, lifetimeSeconds);
        }
    }
}
