using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Tests.Gameplay
{
    public sealed class ExperienceActivationTransactionTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object item in objects) UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
        }

        [Test]
        public void Topology_IsStableAndRejectsMissingOrCycleBeforeActions()
        {
            GameFeatureConfig a = Feature("A");
            GameFeatureConfig b = Feature("B", new[] { "A" });
            GameFeatureConfig c = Feature("C", new[] { "A" });
            ExperienceDefinition experience = Experience(b, c, a);
            var transaction = new ExperienceActivationTransaction(new TestFeatureHost());
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, MapIds(transaction.BuildActivationOrder(experience)));

            GameFeatureConfig missing = Feature("MissingOwner", new[] { "Nope" });
            Assert.Throws<InvalidOperationException>(() => transaction.BuildActivationOrder(Experience(missing)));
            a.Configure("A", new[] { "B" }, null);
            b.Configure("B", new[] { "A" }, null);
            Assert.Throws<InvalidOperationException>(() => transaction.BuildActivationOrder(Experience(a, b)));
        }

        [Test]
        public void ActionFailure_RollsBackReceiptsInReverseOrder()
        {
            var trace = new List<string>();
            TestAction first = Action("first", trace);
            TestAction second = Action("second", trace);
            TestAction failing = Action("fail", trace, true);
            GameFeatureConfig feature = Feature("Feature", null, first, second, failing);
            var transaction = new ExperienceActivationTransaction(new TestFeatureHost());
            Assert.Throws<InvalidOperationException>(() => transaction.Activate(Experience(feature)));
            CollectionAssert.AreEqual(new[] { "+first", "+second", "+fail", "-second", "-first" }, trace);
        }

        [Test]
        public void ReceiptOwnerIdMismatch_IsRejectedAndPriorReceiptsRollback()
        {
            var trace = new List<string>();
            TestAction first = Action("first", trace);
            TestAction wrong = Action("wrong", trace);
            wrong.ReturnWrongOwner = true;
            var transaction = new ExperienceActivationTransaction(new TestFeatureHost());
            Assert.Throws<InvalidOperationException>(() => transaction.Activate(Experience(Feature("Feature", null, first, wrong))));
            CollectionAssert.AreEqual(new[] { "+first", "+wrong", "-wrong", "-first" }, trace);
            Assert.Throws<InvalidOperationException>(() => transaction.Activate(Experience(Feature("Other"))));
        }

        private GameFeatureConfig Feature(string id, string[] dependencies = null, params GameFeatureAction[] actions)
        {
            GameFeatureConfig feature = ScriptableObject.CreateInstance<GameFeatureConfig>();
            objects.Add(feature);
            feature.Configure(id, dependencies, null, actions);
            return feature;
        }

        private ExperienceDefinition Experience(params GameFeatureConfig[] features)
        {
            ExperienceDefinition experience = ScriptableObject.CreateInstance<ExperienceDefinition>();
            objects.Add(experience);
            experience.Configure(features);
            return experience;
        }

        private TestAction Action(string name, List<string> trace, bool fail = false)
        {
            TestAction action = ScriptableObject.CreateInstance<TestAction>();
            objects.Add(action);
            action.Name = name;
            action.Trace = trace;
            action.Fail = fail;
            return action;
        }

        private static string[] MapIds(IReadOnlyList<GameFeatureConfig> features)
        {
            var ids = new string[features.Count];
            for (int index = 0; index < features.Count; index++) ids[index] = features[index].FeatureId;
            return ids;
        }

        private sealed class TestAction : GameFeatureAction
        {
            public string Name;
            public List<string> Trace;
            public bool Fail;
            public bool ReturnWrongOwner;

            public override GameFeatureActivationReceipt Activate(GameFeatureActivationContext context)
            {
                Trace.Add("+" + Name);
                if (Fail) throw new InvalidOperationException(Name);
                Guid ownerId = ReturnWrongOwner ? Guid.NewGuid() : context.OwnerId;
                return new GameFeatureActivationReceipt(ownerId, () => Trace.Add("-" + Name));
            }
        }

        private sealed class TestFeatureHost : IGameFeatureActivationHost
        {
            public World World => null;

            public GameFeatureActivationReceipt InstallComponent(object component, Guid ownerId)
            {
                return new GameFeatureActivationReceipt(ownerId, () => { });
            }
        }
    }
}
