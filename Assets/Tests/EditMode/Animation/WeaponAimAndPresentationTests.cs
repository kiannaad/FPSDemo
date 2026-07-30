using System.Linq;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public class WeaponAimAndPresentationTests
    {
        [Test]
        public void PresentationBinding_ConsumesOnlyMatchingLiveGeneration()
        {
            GameObject prefab = LoadPresentationPrefab();
            GameObject gameObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            WeaponPresentationBinding binding = null;
            try
            {
                WeaponPresentationInstance instance = gameObject.GetComponent<WeaponPresentationInstance>();
                Assert.IsNotNull(instance);
                Assert.IsNotNull(instance.RightHandMount);
                Assert.IsNotNull(instance.LeftHandGrip);
                Assert.IsNotNull(instance.Muzzle);

                binding = instance.CreateBinding(7u);
                Assert.IsTrue(binding.CanConsume(7u));
                Assert.IsFalse(binding.CanConsume(6u));
                Assert.IsFalse(binding.CanConsume(8u));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            Assert.IsFalse(binding.IsAlive);
            Assert.IsFalse(binding.CanConsume(7u));
        }

        [Test]
        public void PresentationBinding_MissingGripDegradesOnlyIk()
        {
            GameObject gameObject = new GameObject("MissingGripPresentation");
            try
            {
                WeaponPresentationInstance instance = gameObject.AddComponent<WeaponPresentationInstance>();
                var binding = new WeaponPresentationBinding(3u, instance, null, gameObject.transform);

                Assert.IsTrue(binding.IsAlive);
                Assert.IsFalse(binding.HasLeftHandGrip);
                Assert.IsFalse(binding.CanConsume(3u));
                Assert.AreSame(gameObject.transform, binding.Muzzle);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CharacterGraph_AimClampsSmoothsAndRejectsOldBinding()
        {
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            WeaponAnimationDefinition definition = config.WeaponDefinitions[0];
            GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(LoadVisualPrefab());
            GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(LoadPresentationPrefab());
            CharacterAnimationGraph graph = null;
            try
            {
                Animator animator = character.GetComponentInChildren<Animator>();
                WeaponPresentationInstance presentation = weapon.GetComponent<WeaponPresentationInstance>();
                Transform rightHand = animator.GetComponentsInChildren<Transform>(true)
                    .First(transform => transform.name == "Right_Hand");
                Assert.IsTrue(presentation.AttachTo(rightHand));
                WeaponPresentationBinding generationOne = presentation.CreateBinding(1u);
                graph = new CharacterAnimationGraph(animator, config);
                graph.ApplyWeaponEquipment(new WeaponEquipmentSnapshot(new WeaponId("rifle"), 1u), generationOne);
                graph.SetAimInput(120f, -80f);

                graph.Update(0.5f);

                Assert.AreEqual(definition.AimYawRange, graph.AimOffset.CurrentYaw, 0.2f);
                Assert.AreEqual(-definition.AimPitchUpRange, graph.AimOffset.CurrentPitch, 0.2f);
                Assert.Greater(graph.AimOffset.CurrentWeight, 0.7f * definition.AimWeight);
                Assert.AreEqual(animator.isHuman ? 1f : 0f, graph.Context.LeftHandIkWeight);

                graph.ApplyWeaponEquipment(new WeaponEquipmentSnapshot(new WeaponId("rifle"), 2u), generationOne);
                graph.Update(0.5f);

                Assert.AreEqual(0f, graph.Context.LeftHandIkWeight);
                if (animator.isHuman)
                {
                    Assert.Less(graph.LeftHandIk.CurrentWeight, 0.01f);
                }
                else
                {
                    Assert.IsNull(graph.LeftHandIk);
                }
                Assert.IsTrue(HasDebugEvent(graph, "LeftHandIkDegraded"));
            }
            finally
            {
                graph?.Dispose();
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(character);
            }
        }

        private static bool HasDebugEvent(CharacterAnimationGraph graph, string eventName)
        {
            foreach (AnimationDebugEvent debugEvent in graph.Context.DebugEvents)
            {
                if (debugEvent.EventName == eventName) return true;
            }
            return false;
        }

        private static GameObject LoadPresentationPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Weapon/KINEMATION/AK/Prefabs/RifleAKPresentation.prefab");
            Assert.IsNotNull(prefab);
            return prefab;
        }

        private static GameObject LoadVisualPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Character/Kinemation/KinemationVisualCharacter.prefab");
            Assert.IsNotNull(prefab);
            return prefab;
        }
    }
}
