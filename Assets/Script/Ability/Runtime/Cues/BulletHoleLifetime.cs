using UnityEngine;

namespace CGame.Ability.Cues
{
    public sealed class BulletHoleLifetime : MonoBehaviour
    {
        private Material material;
        private float lifetimeSeconds;
        private float fadeDurationSeconds;
        private float elapsedSeconds;
        private Color initialColor;

        public void Initialize(Material newMaterial, float newLifetimeSeconds, float newFadeDurationSeconds)
        {
            material = newMaterial;
            lifetimeSeconds = Mathf.Max(0.01f, newLifetimeSeconds);
            fadeDurationSeconds = Mathf.Clamp(newFadeDurationSeconds, 0.01f, lifetimeSeconds);
            initialColor = material == null ? Color.white : material.color;
        }

        public void DisposeImmediately()
        {
            Destroy(gameObject);
        }

        private void Update()
        {
            elapsedSeconds += Time.deltaTime;
            if (material != null && elapsedSeconds >= lifetimeSeconds - fadeDurationSeconds)
            {
                float fadeProgress = Mathf.InverseLerp(lifetimeSeconds - fadeDurationSeconds, lifetimeSeconds, elapsedSeconds);
                material.color = new Color(initialColor.r, initialColor.g, initialColor.b, initialColor.a * (1f - fadeProgress));
            }

            if (elapsedSeconds >= lifetimeSeconds)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }
    }
}
