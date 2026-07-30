using System.Collections.Generic;
using System.Linq;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace CGame.Tests
{
    public class WeaponFireReactionTests
    {
        [Test]
        public void CharacterGraph_ExposesFixedWeaponCompositionOrder()
        {
            CollectionAssert.AreEqual(new[]
            {
                "AnimatorController",
                "WeaponPose",
                "UpperBodyWeaponAction",
                "AimAdditive",
                "AdditiveReaction",
                "LeftHandIK",
            }, CharacterAnimationGraph.CompositionOrder);
        }

        [Test]
        public void ActionNaturalEndReportsPresentationOnlyAndDoesNotCompleteRuntime()
        {
            var runtime = new WeaponRuntime();
            runtime.RequestEquip(new WeaponId("rifle"));
            runtime.RequestFire(out WeaponActionFact fire);
            AnimationClipAsset fireAsset = LoadDefinition().Fire;
            var node = new ActionNode(fireAsset, 10);
            var graph = PlayableGraph.Create("PresentationEndDoesNotCompleteGameplay");
            var contextObject = new GameObject("PresentationEndDoesNotCompleteGameplay");
            var context = new AnimationGraphContext(contextObject.AddComponent<Animator>(), graph);
            var ended = new List<ActionPresentationEnded>();
            node.PresentationEnded += ended.Add;
            try
            {
                node.Initialize(context);
                node.Request(fire.ActionId);
                node.Update(context, 0f);
                node.ClipPlayable.SetTime(fireAsset.AnimationClip.length);
                node.Update(context, 0f);

                Assert.AreEqual(1, ended.Count);
                Assert.AreEqual(fire.ActionId, ended[0].RequestId);
                Assert.AreEqual(ActionPresentationEndReason.NaturalEnd, ended[0].Reason);
                Assert.AreEqual(fire.ActionId, runtime.ActiveAction.ActionId);
                Assert.AreEqual(WeaponActionPhase.Started, runtime.ActiveAction.Phase);
            }
            finally
            {
                node.Destroy();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
                Object.DestroyImmediate(contextObject);
            }
        }

        [Test]
        public void CharacterGraph_FireActionKeepsAimAndRecoilStacksOnlyUniqueCommittedIds()
        {
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(LoadVisualPrefab());
            GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(LoadDefinition().PresentationPrefab);
            CharacterAnimationGraph graph = null;
            try
            {
                Animator animator = character.GetComponentInChildren<Animator>();
                WeaponPresentationInstance presentation = weapon.GetComponent<WeaponPresentationInstance>();
                Transform rightHand = animator.GetComponentsInChildren<Transform>(true)
                    .First(transform => transform.name == "Right_Hand");
                presentation.AttachTo(rightHand);
                graph = new CharacterAnimationGraph(animator, config);
                graph.ApplyWeaponEquipment(
                    new WeaponEquipmentSnapshot(new WeaponId("rifle"), 1u),
                    presentation.CreateBinding(1u));
                graph.SetAimInput(35f, -20f);
                var first = new WeaponActionFact(1ul, 1u, new WeaponId("rifle"), WeaponActionKind.Fire, WeaponActionPhase.Started);
                var second = new WeaponActionFact(2ul, 1u, new WeaponId("rifle"), WeaponActionKind.Fire, WeaponActionPhase.Started);

                Assert.IsTrue(graph.StartWeaponAction(first));
                Assert.IsTrue(graph.CommitFireReaction(first));
                Assert.IsFalse(graph.CommitFireReaction(first));
                graph.Update(0f);
                float firstPitch = graph.RecoilReaction.CurrentPitch;
                Assert.IsTrue(graph.FireAction.IsActive);
                Assert.AreEqual(first.ActionId, graph.FireAction.RequestId);
                Assert.AreEqual(1f, graph.Context.AimWeight);

                Assert.IsTrue(graph.StartWeaponAction(second));
                Assert.IsTrue(graph.CommitFireReaction(second));
                Assert.Greater(graph.RecoilReaction.CurrentPitch, firstPitch);

                WeaponActionFact cancelled = second.End(WeaponActionPhase.Cancelled, WeaponActionEndReason.Unequipped);
                graph.Update(0f);
                Assert.IsTrue(graph.EndWeaponAction(cancelled));
                Assert.AreEqual(0f, graph.RecoilReaction.CurrentPitch);
            }
            finally
            {
                graph?.Dispose();
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void GameplayCanEndPendingPresentationBeforeGraphTickWithoutGhostAction()
        {
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(LoadVisualPrefab());
            CharacterAnimationGraph graph = null;
            try
            {
                graph = new CharacterAnimationGraph(character.GetComponentInChildren<Animator>(), config);
                graph.ApplyWeaponEquipment(new WeaponEquipmentSnapshot(new WeaponId("rifle"), 1u));
                var started = new WeaponActionFact(5ul, 1u, new WeaponId("rifle"), WeaponActionKind.Fire, WeaponActionPhase.Started);
                var completed = started.End(WeaponActionPhase.Completed, WeaponActionEndReason.Completed);
                var ended = new List<ActionPresentationEnded>();
                graph.PresentationEnded += ended.Add;

                Assert.IsTrue(graph.StartWeaponAction(started));
                Assert.IsTrue(graph.EndWeaponAction(completed));
                graph.Update(0.1f);

                Assert.IsFalse(graph.FireAction.IsActive);
                Assert.AreEqual(0ul, graph.FireAction.PendingRequestId);
                Assert.AreEqual(ActionPresentationEndReason.GameplayCompleted, ended[0].Reason);
            }
            finally
            {
                graph?.Dispose();
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void ModelActionPlayer_UsesRealAkMechanismClipAndStopsByActionId()
        {
            WeaponAnimationDefinition definition = LoadDefinition();
            GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(definition.PresentationPrefab);
            try
            {
                WeaponModelActionPlayer player = weapon.GetComponent<WeaponPresentationInstance>().ModelActionPlayer;
                Assert.IsNotNull(player);
                Assert.IsNotNull(definition.WeaponModelFire);
                Assert.AreEqual("A_W_AKX_Fire", definition.WeaponModelFire.AnimationClip.name);
                Assert.IsTrue(player.Play(definition.WeaponModelFire.AnimationClip, 9ul));
                Assert.IsTrue(player.IsPlaying);
                Assert.IsFalse(player.Stop(8ul));
                Assert.IsTrue(player.Stop(9ul));
                Assert.IsFalse(player.IsPlaying);
            }
            finally
            {
                Object.DestroyImmediate(weapon);
            }
        }

        private static WeaponAnimationDefinition LoadDefinition()
        {
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            Assert.IsNotNull(config);
            Assert.AreEqual(1, config.WeaponDefinitions.Length);
            return config.WeaponDefinitions[0];
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
