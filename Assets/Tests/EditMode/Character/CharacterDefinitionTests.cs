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
        public void Validate_AcceptsMatchingExpectedId()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            try
            {
                Assert.AreEqual(
                    CharacterDefinitionResolveError.None,
                    definition.Validate(
                        new CharacterDefinitionId("local-player")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Validate_ReturnsDefinitionIdMismatchForUnexpectedId()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            try
            {
                Assert.AreEqual(
                    CharacterDefinitionResolveError.DefinitionIdMismatch,
                    definition.Validate(
                        new CharacterDefinitionId(
                            "different-player")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Validate_ReturnsExplicitAssetErrors()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            try
            {
                SetField(definition, "visualPrefab", null);
                Assert.AreEqual(
                    CharacterDefinitionResolveError.MissingVisualPrefab,
                    definition.Validate());

                SetField(definition, "visualPrefab", LoadVisualPrefab());
                SetField(definition, "animationConfig", null);
                Assert.AreEqual(
                    CharacterDefinitionResolveError
                        .InvalidAnimationConfig,
                    definition.Validate());
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
