using System.Collections.Generic;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class WeaponReloadCurveTests
    {
        [Test]
        public void Runtime_ReloadPublishesStartedCompletedAndExplicitCancellation()
        {
            var runtime = new WeaponRuntime();
            var facts = new List<WeaponActionFact>();
            runtime.ActionChanged += facts.Add;

            Assert.IsFalse(runtime.RequestReload(out _));
            Assert.IsTrue(runtime.Initialize(
                new WeaponId("rifle"),
                new WeaponRuntimeCapabilities(true, true, false)));
            Assert.IsTrue(runtime.RequestReload(out WeaponActionFact first, 12.5d));
            Assert.AreEqual(WeaponActionKind.Reload, first.Kind);
            Assert.AreEqual(12.5d, first.AuthoritativeStartTime);
            Assert.IsTrue(runtime.CompleteAction(first.ActionId));
            Assert.AreEqual(WeaponActionPhase.Completed, facts[1].Phase);

            Assert.IsTrue(runtime.RequestReload(out WeaponActionFact second));
            Assert.IsTrue(runtime.RequestFire(out WeaponActionFact fire));
            Assert.AreEqual(second.ActionId, facts[3].ActionId);
            Assert.AreEqual(WeaponActionEndReason.Superseded, facts[3].EndReason);
            Assert.AreEqual(fire.ActionId, runtime.ActiveAction.ActionId);
        }

        [Test]
        public void ReloadAsset_ProvidesAttachIkAndWeaponBoneCurvesAtBoundaries()
        {
            WeaponAnimationDefinition definition = LoadDefinition();
            AnimationClipAsset reload = definition.Reload;
            Assert.IsTrue(reload.TryGetNamedCurve("MaskLeftHandIK", out AnimationCurve ik));
            Assert.IsTrue(reload.TryGetNamedCurve("MaskAttachHand", out AnimationCurve attach));
            Assert.IsTrue(reload.TryGetNamedCurve("WeaponBoneWeight", out AnimationCurve weapon));

            Assert.AreEqual(1f, ik.Evaluate(0f), 0.001f);
            Assert.AreEqual(0f, ik.Evaluate(0.5f), 0.001f);
            Assert.AreEqual(1f, ik.Evaluate(1f), 0.001f);
            Assert.AreEqual(0f, attach.Evaluate(0.5f), 0.001f);
            Assert.AreEqual(1f, weapon.Evaluate(0.5f), 0.001f);
            Assert.IsFalse(reload.AnimationClip.isLooping);
        }

        [Test]
        public void Graph_ReloadOutranksEquipAndFireAndEndsWithoutStaleCurves()
        {
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(LoadVisualPrefab());
            CharacterAnimationGraph graph = null;
            try
            {
                graph = new CharacterAnimationGraph(character.GetComponentInChildren<Animator>(), config);
                graph.ApplyWeaponEquipment(new WeaponEquipmentSnapshot(new WeaponId("rifle"), 1u));
                var reload = new WeaponActionFact(
                    7ul, 1u, new WeaponId("rifle"), WeaponActionKind.Reload, WeaponActionPhase.Started);
                var fire = new WeaponActionFact(
                    8ul, 1u, new WeaponId("rifle"), WeaponActionKind.Fire, WeaponActionPhase.Started);

                Assert.IsTrue(graph.StartWeaponAction(reload));
                Assert.IsTrue(graph.StartWeaponAction(fire));
                graph.Update(0.9f);
                Assert.IsTrue(graph.ReloadAction.IsActive);
                Assert.IsFalse(graph.FireAction.IsActive, "lower-priority fire must be interrupted by reload");
                Assert.Less(graph.Context.LeftHandIkWeight, 0.01f);
                Assert.AreEqual(1f, graph.Context.WeaponBoneWeight, 0.001f);

                Assert.IsTrue(graph.EndWeaponAction(
                    reload.End(WeaponActionPhase.Cancelled, WeaponActionEndReason.Cancelled)));
                graph.Update(0f);
                Assert.IsFalse(graph.ReloadAction.IsActive);
                Assert.AreEqual(1f, graph.Context.WeaponBoneWeight, 0.001f);
            }
            finally
            {
                graph?.Dispose();
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void Bridge_StartsCharacterAndWeaponReloadWithSameActionId()
        {
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(LoadVisualPrefab());
            CharacterAnimationGraph graph = null;
            CharacterWeaponAnimationBridge bridge = null;
            try
            {
                Animator animator = character.GetComponentInChildren<Animator>();
                graph = new CharacterAnimationGraph(animator, config);
                bridge = new CharacterWeaponAnimationBridge(animator, config, graph);
                var runtime = new WeaponRuntime();
                runtime.RequestEquip(new WeaponId("rifle"));
                bridge.Update(runtime.Snapshot, 0f, 0f, 0f);
                bridge.BindRuntime(runtime);

                Assert.IsTrue(runtime.RequestReload(out WeaponActionFact reload));
                Assert.AreEqual(reload.ActionId, graph.ReloadAction.PendingRequestId);
                Assert.AreEqual(reload.ActionId, bridge.CurrentPresentation.ModelActionPlayer.ActionId);

                Assert.IsTrue(runtime.CancelAction(reload.ActionId));
                Assert.IsFalse(bridge.CurrentPresentation.ModelActionPlayer.IsPlaying);
            }
            finally
            {
                bridge?.Dispose();
                graph?.Dispose();
                Object.DestroyImmediate(character);
            }
        }

        private static WeaponAnimationDefinition LoadDefinition()
        {
            return AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                "Assets/Art/Animation/Weapon/KINEMATION/AK/RifleAKAnimationDefinition.asset");
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
