using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Tests.Gameplay
{
    public sealed class ExperienceManagerComponentTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private World world;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
        }

        [TearDown]
        public void TearDown()
        {
            if (world != null) world.ShutdownAsync().GetAwaiter().GetResult();
            foreach (UnityEngine.Object item in objects) UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
        }

        [Test]
        public void ActionComponentConflict_FailsAndRollsBackExactComponent()
        {
            var trace = new List<string>();
            InstallAction first = Action(trace, "first");
            InstallAction conflict = Action(trace, "conflict");
            ExperienceManagerComponent manager = Manager(Feature("Feature", null, first, conflict));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("component type InstalledComponent"));
            AssertFails(manager.LoadAsync());
            Assert.AreEqual(ExperienceLoadState.Failed, manager.LoadState);
            Assert.AreEqual(1, manager.FailedWriteCount);
            Assert.AreEqual(0, manager.ReadyWriteCount);
            Assert.AreEqual(0, manager.Components.Count);
            CollectionAssert.AreEqual(new[] { "+first", "+conflict", "-first" }, trace);
        }

        [Test]
        public void ResourceFailure_PreventsAllActionsAndReleasesPriorLease()
        {
            var trace = new List<string>();
            var loader = new SequencedLoader(trace, failSecond: true);
            ExperienceManagerComponent manager = Manager(
                loader,
                Feature("A", new[] { "asset-a" }, Action(trace, "action")),
                Feature("B", new[] { "asset-b" }));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("resource failure"));
            AssertFails(manager.LoadAsync());
            CollectionAssert.AreEqual(new[] { "+asset-a", "+asset-b", "-asset-a" }, trace);
            Assert.AreEqual(ExperienceLoadState.Failed, manager.LoadState);
            Assert.AreEqual(1, manager.FailedWriteCount);
            Assert.AreEqual(0, manager.ReadyWriteCount);
        }

        [Test]
        public void Shutdown_WaitsForLoadThenSkipsActionAndReadyAndReleasesLease()
        {
            var trace = new List<string>();
            var loader = new DelayedLoader(trace);
            ExperienceManagerComponent manager = Manager(
                loader,
                Feature("Feature", new[] { "asset" }, Action(trace, "action")));

            Task loadTask = manager.LoadAsync();
            Task shutdownTask = manager.ShutdownAsync();
            Assert.IsFalse(shutdownTask.IsCompleted);
            loader.Complete();
            shutdownTask.GetAwaiter().GetResult();
            loadTask.GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "+asset", "-asset" }, trace);
            Assert.AreEqual(ExperienceLoadState.Shutdown, manager.LoadState);
            Assert.AreEqual(0, manager.ReadyWriteCount);
            Assert.AreEqual(0, manager.FailedWriteCount);
        }

        private ExperienceManagerComponent Manager(params GameFeatureConfig[] features) =>
            new ExperienceManagerComponent(world, Experience(features));

        private ExperienceManagerComponent Manager(IExperienceAssetLoader loader, params GameFeatureConfig[] features) =>
            new ExperienceManagerComponent(world, Experience(features), loader);

        private static void AssertFails(Task task)
        {
            try
            {
                task.GetAwaiter().GetResult();
                Assert.Fail("Expected InvalidOperationException.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        private GameFeatureConfig Feature(string id, string[] locations = null, params GameFeatureAction[] actions)
        {
            GameFeatureConfig feature = ScriptableObject.CreateInstance<GameFeatureConfig>();
            objects.Add(feature);
            feature.Configure(id, null, locations, actions);
            return feature;
        }

        private ExperienceDefinition Experience(params GameFeatureConfig[] features)
        {
            ExperienceDefinition experience = ScriptableObject.CreateInstance<ExperienceDefinition>();
            objects.Add(experience);
            experience.Configure(features);
            return experience;
        }

        private InstallAction Action(List<string> trace, string name)
        {
            InstallAction action = ScriptableObject.CreateInstance<InstallAction>();
            objects.Add(action);
            action.Trace = trace;
            action.Name = name;
            return action;
        }

        private sealed class InstalledComponent : IDisposable
        {
            private readonly List<string> trace;
            private readonly string name;
            public InstalledComponent(List<string> trace, string name) { this.trace = trace; this.name = name; }
            public void Dispose() => trace.Add("-" + name);
        }

        private sealed class InstallAction : GameFeatureAction
        {
            public List<string> Trace;
            public string Name;
            public override GameFeatureActivationReceipt Activate(GameFeatureActivationContext context)
            {
                Trace.Add("+" + Name);
                return context.InstallComponent(new InstalledComponent(Trace, Name));
            }
        }

        private sealed class SequencedLoader : IExperienceAssetLoader
        {
            private readonly List<string> trace;
            private readonly bool failSecond;
            private int calls;
            public SequencedLoader(List<string> trace, bool failSecond) { this.trace = trace; this.failSecond = failSecond; }
            public Task<IDisposable> LoadAsync(IReadOnlyList<string> locations)
            {
                calls++;
                string location = locations[0];
                trace.Add("+" + location);
                if (failSecond && calls == 2) throw new InvalidOperationException("resource failure");
                return Task.FromResult<IDisposable>(new Lease(() => trace.Add("-" + location)));
            }
        }

        private sealed class DelayedLoader : IExperienceAssetLoader
        {
            private readonly List<string> trace;
            private readonly TaskCompletionSource<IDisposable> completion = new TaskCompletionSource<IDisposable>();
            public DelayedLoader(List<string> trace) { this.trace = trace; }
            public Task<IDisposable> LoadAsync(IReadOnlyList<string> locations)
            {
                trace.Add("+" + locations[0]);
                return completion.Task;
            }
            public void Complete() => completion.SetResult(new Lease(() => trace.Add("-asset")));
        }

        private sealed class Lease : IDisposable
        {
            private Action release;
            public Lease(Action release) { this.release = release; }
            public void Dispose() { Action action = release; release = null; action?.Invoke(); }
        }
    }
}
