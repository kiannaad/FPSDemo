using System.Collections.Generic;
using UnityEngine;

namespace CGame.Ability.Cues
{
    [CreateAssetMenu(fileName = "BulletHoleCueNotify", menuName = "CGame/Ability/Bullet Hole Cue Notify")]
    public sealed class BulletHoleCueNotifyDefinition : CueNotifyDefinition
    {
        [SerializeField] private Texture2D bulletHoleTexture;
        [SerializeField, Min(0.01f)] private float lifetimeSeconds = 8f;
        [SerializeField, Min(0.01f)] private float fadeDurationSeconds = 1f;
        [SerializeField, Min(0.001f)] private float size = 0.075f;
        [SerializeField, Min(0.0001f)] private float surfaceOffset = 0.002f;
        [SerializeField, Min(1)] private int maxActiveBulletHoles = 24;

        private readonly List<BulletHoleLifetime> activeBulletHoles = new List<BulletHoleLifetime>();

        public int ActiveBulletHoleCount
        {
            get
            {
                RemoveDestroyedBulletHoles();
                return activeBulletHoles.Count;
            }
        }

        public void Configure(
            Texture2D texture,
            float newLifetimeSeconds,
            float newFadeDurationSeconds,
            float newSize,
            float newSurfaceOffset,
            int newMaxActiveBulletHoles)
        {
            bulletHoleTexture = texture;
            lifetimeSeconds = Mathf.Max(0.01f, newLifetimeSeconds);
            fadeDurationSeconds = Mathf.Clamp(newFadeDurationSeconds, 0.01f, lifetimeSeconds);
            size = Mathf.Max(0.001f, newSize);
            surfaceOffset = Mathf.Max(0.0001f, newSurfaceOffset);
            maxActiveBulletHoles = Mathf.Max(1, newMaxActiveBulletHoles);
        }

        public override void HandleCue(GameplayCueEventType eventType, GameplayCueParameters parameters, GameplayCueHandle handle)
        {
            if (eventType != GameplayCueEventType.Executed || !parameters.HasLocation || !parameters.HasNormal)
            {
                return;
            }

            Vector3 normal = parameters.Normal.sqrMagnitude > 0.0001f ? parameters.Normal.normalized : Vector3.up;
            RemoveDestroyedBulletHoles();
            while (activeBulletHoles.Count >= maxActiveBulletHoles)
            {
                BulletHoleLifetime oldest = activeBulletHoles[0];
                activeBulletHoles.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.DisposeImmediately();
                }
            }

            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Quad);
            instance.name = "GameplayCueBulletHole";
            Quaternion surfaceRotation = Quaternion.LookRotation(-normal, Vector3.up);
            instance.transform.SetPositionAndRotation(
                parameters.Location + normal * surfaceOffset,
                surfaceRotation);
            instance.transform.localScale = Vector3.one * size;

            Collider collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Material material = CreateMaterial();
            Renderer renderer = instance.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            BulletHoleLifetime lifetime = instance.AddComponent<BulletHoleLifetime>();
            lifetime.Initialize(material, lifetimeSeconds, fadeDurationSeconds);
            activeBulletHoles.Add(lifetime);
        }

        private Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                Debug.LogError("Bullet-hole material creation failed because no supported transparent shader was found.");
            }

            Material material = new Material(shader)
            {
                mainTexture = bulletHoleTexture,
                color = Color.white
            };
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", bulletHoleTexture);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_Blend"))
                {
                    material.SetFloat("_Blend", 0f);
                }

                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                }

                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }

                if (material.HasProperty("_SrcBlendAlpha"))
                {
                    material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                }

                if (material.HasProperty("_DstBlendAlpha"))
                {
                    material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 0f);
                }

                if (material.HasProperty("_AlphaToMask"))
                {
                    material.SetFloat("_AlphaToMask", 0f);
                }

                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetShaderPassEnabled("DepthOnly", false);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return material;
        }

        private void RemoveDestroyedBulletHoles()
        {
            activeBulletHoles.RemoveAll(candidate => candidate == null);
        }
    }
}
