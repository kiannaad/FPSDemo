using CGame.Animation;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Weapon.Equipment.PlayMode.Tests
{
    public sealed class CharacterPresentationVisibilityPlayModeTests
    {
        [Test]
        public void FirstPersonEquip_DoesNotDisableCharacterRendererGlobally()
        {
            var character = new GameObject("Character");
            var cameraObject = new GameObject("Main Camera");
            var weaponPrefab = new GameObject("WeaponPrefab");
            WeaponAnimationDefinition definition = null;
            CharacterWeaponPresentationController controller = null;
            try
            {
                Animator animator = character.AddComponent<Animator>();
                var bodyObject = new GameObject("Body");
                bodyObject.transform.SetParent(character.transform, false);
                SkinnedMeshRenderer body =
                    bodyObject.AddComponent<SkinnedMeshRenderer>();
                body.enabled = true;

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();

                var mount = new GameObject("RightHandMount");
                mount.transform.SetParent(weaponPrefab.transform, false);
                WeaponPresentationInstance presentation =
                    weaponPrefab.AddComponent<WeaponPresentationInstance>();
                typeof(WeaponPresentationInstance)
                    .GetField(
                        "rightHandMount",
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(presentation, mount.transform);

                definition =
                    ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
                typeof(WeaponAnimationDefinition)
                    .GetField(
                        "weaponId",
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(definition, "rifle");
                typeof(WeaponAnimationDefinition)
                    .GetField(
                        "weaponPrefab",
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(definition, weaponPrefab);

                controller =
                    new CharacterWeaponPresentationController(animator);
                Assert.That(controller.TryEquip(definition, 1u), Is.True);

                Assert.That(body.enabled, Is.True,
                    "First-person presentation must not mutate the character renderer's persistent enabled state.");
            }
            finally
            {
                controller?.Dispose();
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }

                Object.DestroyImmediate(weaponPrefab);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(character);
            }
        }
    }
}
