using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace CGame.Tests
{
    public class WeaponAnimationLayerTests
    {
        [Test]
        public void BlendNode_RapidRetargetKeepsOnlyCurrentAndLatestNext()
        {
            WeaponAnimationDefinition definition = CreateDefinition(includeStop: false);
            string locomotionState = "Idle";
            using (var fixture = new GraphFixture())
            {
                var blendNode = new WeaponLayerBlendNode(
                    new ClipNode(definition.Idle.AnimationClip),
                    CreateUpperBodyMask());
                var first = new WeaponAnimationLayer(definition, 1u, () => locomotionState);
                var latest = new WeaponAnimationLayer(definition, 2u, () => locomotionState);
                try
                {
                    blendNode.Initialize(fixture.Context);
                    blendNode.SetTarget(first, 0.1f);
                    blendNode.Update(fixture.Context, 0.05f);
                    blendNode.Evaluate(fixture.Context);

                    Assert.AreSame(first, blendNode.NextLayer);
                    Assert.AreEqual(0.5f, blendNode.NextWeight, 0.001f);

                    blendNode.SetTarget(latest, 0.1f);

                    Assert.IsTrue(first.IsDisposed);
                    Assert.AreSame(latest, blendNode.NextLayer);
                    blendNode.Update(fixture.Context, 0.1f);
                    Playable fullyEquipped = blendNode.Evaluate(fixture.Context);
                    Assert.AreSame(latest, blendNode.CurrentLayer);
                    Assert.IsNull(blendNode.NextLayer);
                    Assert.AreEqual(1f, fullyEquipped.GetInputWeight(0), 0.001f,
                        "The locomotion base layer must remain enabled beneath the weapon mask.");

                    locomotionState = "Stop";
                    blendNode.Update(fixture.Context, 0.016f);
                    Playable output = blendNode.Evaluate(fixture.Context);
                    Assert.IsFalse(latest.IsPoseAvailable);
                    Assert.AreEqual(1f, output.GetInputWeight(0), 0.001f);
                    Assert.AreEqual(0f, blendNode.CurrentWeight, 0.001f);

                    blendNode.SetTarget(null, 0.1f);
                    blendNode.Update(fixture.Context, 0.1f);
                    blendNode.Evaluate(fixture.Context);
                    Assert.IsTrue(latest.IsDisposed);
                    Assert.IsNull(blendNode.CurrentLayer);
                }
                finally
                {
                    blendNode.Destroy();
                }
            }

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Layers_FromSameDefinitionKeepIndependentRuntimeState()
        {
            WeaponAnimationDefinition definition = CreateDefinition(includeStop: false);
            string firstState = "Idle";
            string secondState = "Move";
            using (var fixture = new GraphFixture())
            {
                var first = new WeaponAnimationLayer(definition, 10u, () => firstState);
                var second = new WeaponAnimationLayer(definition, 11u, () => secondState);
                try
                {
                    first.Initialize(fixture.Context);
                    second.Initialize(fixture.Context);
                    first.Update(fixture.Context, 0.016f);
                    second.Update(fixture.Context, 0.016f);
                    first.Evaluate(fixture.Context);
                    second.Evaluate(fixture.Context);

                    Assert.AreEqual("Idle", first.SelectedLocomotionState);
                    Assert.AreEqual("Move", second.SelectedLocomotionState);
                    Assert.AreEqual(10u, first.Generation);
                    Assert.AreEqual(11u, second.Generation);
                    Assert.IsTrue(first.IsPoseAvailable);
                    Assert.IsTrue(second.IsPoseAvailable);
                }
                finally
                {
                    first.Destroy();
                    second.Destroy();
                }
            }

            Object.DestroyImmediate(definition);
        }

        private static WeaponAnimationDefinition CreateDefinition(bool includeStop)
        {
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            Assert.IsNotNull(config);
            var definition = ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
            var serializedDefinition = new SerializedObject(definition);
            WeaponAnimationDefinition source = config.WeaponDefinitions[0];
            serializedDefinition.FindProperty("weaponId").stringValue = "rifle";
            serializedDefinition.FindProperty("idle").objectReferenceValue = source.Idle;
            serializedDefinition.FindProperty("walk").objectReferenceValue = source.Walk;
            serializedDefinition.FindProperty("run").objectReferenceValue = source.Run;
            serializedDefinition.FindProperty("stop").objectReferenceValue = includeStop ? source.Stop : null;
            serializedDefinition.FindProperty("equip").objectReferenceValue = source.Equip;
            serializedDefinition.FindProperty("unequip").objectReferenceValue = source.Unequip;
            serializedDefinition.FindProperty("fire").objectReferenceValue = source.Fire;
            serializedDefinition.FindProperty("weaponModelFire").objectReferenceValue = source.WeaponModelFire;
            serializedDefinition.FindProperty("reload").objectReferenceValue = source.Reload;
            serializedDefinition.FindProperty("weaponModelReload").objectReferenceValue = source.WeaponModelReload;
            serializedDefinition.FindProperty("blendDuration").floatValue = 0.1f;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(definition.IsValid);
            return definition;
        }

        private static AvatarMask CreateUpperBodyMask()
        {
            var mask = new AvatarMask();
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            return mask;
        }

        private sealed class GraphFixture : System.IDisposable
        {
            private readonly GameObject gameObject;

            public GraphFixture()
            {
                gameObject = new GameObject("WeaponAnimationLayerFixture");
                Animator animator = gameObject.AddComponent<Animator>();
                Graph = PlayableGraph.Create("WeaponAnimationLayerFixture");
                Context = new AnimationGraphContext(animator, Graph);
            }

            public PlayableGraph Graph { get; }
            public AnimationGraphContext Context { get; }

            public void Dispose()
            {
                if (Graph.IsValid())
                {
                    Graph.Destroy();
                }

                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
