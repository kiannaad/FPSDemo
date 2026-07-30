using System;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class CharacterDefinitionTests
    {
        [Test]
        public void DefinitionId_UsesExactValueEquality()
        {
            var first = new CharacterDefinitionId("local-player");
            var equal = new CharacterDefinitionId("local-player");
            var differentCase = new CharacterDefinitionId("Local-Player");

            Assert.IsTrue(first.IsValid);
            Assert.AreEqual(first, equal);
            Assert.AreNotEqual(first, differentCase);
            Assert.IsFalse(new CharacterDefinitionId(" ").IsValid);
        }

        [Test]
        public void Provider_ResolvesRegisteredDefinitionByIdOnly()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            try
            {
                var provider = new InMemoryCharacterDefinitionProvider(new[] { definition });

                CharacterDefinitionResolveResult result = provider.Resolve(new CharacterDefinitionId("local-player"));

                Assert.IsTrue(result.IsSuccess);
                Assert.AreSame(definition, result.Definition);
                Assert.AreEqual(CharacterDefinitionResolveError.None, result.Error);
                Assert.AreEqual(typeof(CharacterDefinitionId), typeof(ICharacterDefinitionProvider)
                    .GetMethod(nameof(ICharacterDefinitionProvider.Resolve))
                    .GetParameters()
                    .Single()
                    .ParameterType);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Provider_ReturnsExplicitErrorsForInvalidAndUnknownIds()
        {
            var provider = new InMemoryCharacterDefinitionProvider(Array.Empty<CharacterDefinition>());

            Assert.AreEqual(CharacterDefinitionResolveError.InvalidDefinitionId, provider.Resolve(default).Error);
            Assert.AreEqual(CharacterDefinitionResolveError.DefinitionNotFound, provider.Resolve(new CharacterDefinitionId("unknown")).Error);
        }

        [Test]
        public void Provider_ReturnsDefinitionIdMismatchWhenAssetChangesAfterRegistration()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            try
            {
                var provider = new InMemoryCharacterDefinitionProvider(new[] { definition });
                SetField(definition, "definitionId", "different-player");

                CharacterDefinitionResolveResult result = provider.Resolve(new CharacterDefinitionId("local-player"));

                Assert.IsFalse(result.IsSuccess);
                Assert.AreEqual(CharacterDefinitionResolveError.DefinitionIdMismatch, result.Error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Provider_ReturnsExplicitErrorsForMissingVisualAndInvalidAnimationConfig()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            try
            {
                var provider = new InMemoryCharacterDefinitionProvider(new[] { definition });
                SetField(definition, "visualPrefab", null);
                Assert.AreEqual(CharacterDefinitionResolveError.MissingVisualPrefab, provider.Resolve(new CharacterDefinitionId("local-player")).Error);

                SetField(definition, "visualPrefab", LoadVisualPrefab());
                SetField(definition, "animationConfig", null);
                Assert.AreEqual(CharacterDefinitionResolveError.InvalidAnimationConfig, provider.Resolve(new CharacterDefinitionId("local-player")).Error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static CharacterDefinition CreateValidDefinition(string definitionId)
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            SetField(definition, "definitionId", definitionId);
            SetField(definition, "visualPrefab", LoadVisualPrefab());
            SetField(definition, "animationConfig", Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig"));
            SetField(definition, "supportedControlKinds", new[] { CharacterControlKind.LocalPlayer });
            SetField(definition, "initialWeaponId", "knife");
            Assert.IsTrue(definition.IsValid);
            return definition;
        }

        private static GameObject LoadVisualPrefab()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Animation/FemaleLocomotionSet/Prefabs/Robot Kyle.prefab");
        }

        private static void SetField(CharacterDefinition definition, string name, object value)
        {
            FieldInfo field = typeof(CharacterDefinition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(definition, value);
        }
    }
}
