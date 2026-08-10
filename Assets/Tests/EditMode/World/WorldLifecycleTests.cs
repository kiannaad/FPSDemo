using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CGame.WorldRuntime.Tests
{
    public sealed class WorldLifecycleTests
    {
        [TearDown]
        public void TearDown()
        {
            if (World.Current != null)
            {
                World.Current.ShutdownAsync().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void InitializeStartPlayAndShutdown_UseDependencyAndReverseOrder()
        {
            var trace = new List<string>();
            World world = World.Create(new WorldSubSystem[]
            {
                new DependentSubSystem(trace),
                new FoundationSubSystem(trace)
            });

            world.InitializeAsync().GetAwaiter().GetResult();
            Assert.That(world.State, Is.EqualTo(WorldState.Initialized));
            world.UpdateTick(0.25f);
            CollectionAssert.DoesNotContain(trace, "dependent.tick");

            world.StartPlay();
            world.UpdateTick(0.25f);
            Task firstShutdown = world.ShutdownAsync();
            Task secondShutdown = world.ShutdownAsync();
            Assert.That(secondShutdown, Is.SameAs(firstShutdown));
            firstShutdown.GetAwaiter().GetResult();

            Assert.That(trace, Is.EqualTo(new[]
            {
                "foundation.initialize",
                "dependent.initialize",
                "foundation.begin",
                "dependent.begin",
                "dependent.tick",
                "dependent.end",
                "foundation.end",
                "dependent.shutdown",
                "foundation.shutdown"
            }));
            Assert.That(world.State, Is.EqualTo(WorldState.Destroyed));
            Assert.That(World.Current, Is.Null);
        }

        [Test]
        public void Create_RejectsDuplicateMissingAndCyclicSubSystems()
        {
            Assert.Throws<InvalidOperationException>(() => World.Create(new WorldSubSystem[]
            {
                new FoundationSubSystem(new List<string>()),
                new FoundationSubSystem(new List<string>())
            }));
            Assert.Throws<InvalidOperationException>(() => World.Create(new WorldSubSystem[]
            {
                new DependentSubSystem(new List<string>())
            }));
            Assert.Throws<InvalidOperationException>(() => World.Create(new WorldSubSystem[]
            {
                new CycleASubSystem(),
                new CycleBSubSystem()
            }));
        }

        [Test]
        public void InitializeFailure_RollsBackEarlierSubSystemsAndKeepsFaultedUntilShutdown()
        {
            var trace = new List<string>();
            World world = World.Create(new WorldSubSystem[]
            {
                new FoundationSubSystem(trace),
                new FailingSubSystem(trace)
            });

            InvalidOperationException failure = null;
            try
            {
                world.InitializeAsync().GetAwaiter().GetResult();
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            Assert.That(failure, Is.Not.Null);

            Assert.That(world.State, Is.EqualTo(WorldState.Faulted));
            Assert.That(trace, Is.EqualTo(new[]
            {
                "foundation.initialize",
                "failing.initialize",
                "foundation.shutdown"
            }));
            world.ShutdownAsync().GetAwaiter().GetResult();
            world.ShutdownAsync().GetAwaiter().GetResult();
            Assert.That(World.Current, Is.Null);
        }

        [Test]
        public void CriticalSubSystemTickFault_EndsPlayAfterDomainAndStopsLaterTicks()
        {
            var trace = new List<string>();
            World world = World.Create(new[] { new FaultingSubSystem(trace) });
            world.InitializeAsync().GetAwaiter().GetResult();
            world.StartPlay();

            world.UpdateTick(1f);
            world.UpdateTick(1f);

            Assert.That(world.State, Is.EqualTo(WorldState.Faulted));
            Assert.That(trace, Is.EqualTo(new[] { "begin", "tick", "end" }));
            Assert.That(world.Failure, Is.EqualTo("expected"));
        }

        [Test]
        public void StartPlayFailure_EndsOnlyBegunSubSystemsInReverseAndLeavesWorldFaulted()
        {
            var trace = new List<string>();
            World world = World.Create(new WorldSubSystem[]
            {
                new FoundationSubSystem(trace),
                new BeginFailingSubSystem(trace)
            });
            world.InitializeAsync().GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() => world.StartPlay());

            Assert.That(world.State, Is.EqualTo(WorldState.Faulted));
            Assert.That(trace, Does.Contain("foundation.end"));
            CollectionAssert.DoesNotContain(trace, "failing.end");
        }

        [Test]
        public void InitializeCancellation_RollsBackAndSecondWorldIsRejectedUntilShutdown()
        {
            var trace = new List<string>();
            var cancellation = new CancellationTokenSource();
            World world = World.Create(new WorldSubSystem[]
            {
                new FoundationSubSystem(trace),
                new CancelingSubSystem(cancellation, trace)
            });
            Assert.Throws<InvalidOperationException>(() => World.Create());

            OperationCanceledException canceled = null;
            try
            {
                world.InitializeAsync(cancellation.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException exception)
            {
                canceled = exception;
            }

            Assert.That(canceled, Is.Not.Null);
            Assert.That(world.State, Is.EqualTo(WorldState.Faulted));
            Assert.That(trace, Does.Contain("foundation.shutdown"));
        }

        private sealed class FoundationSubSystem : WorldSubSystem
        {
            private readonly List<string> trace;

            public FoundationSubSystem(List<string> trace) => this.trace = trace;

            protected override Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                trace.Add("foundation.initialize");
                return Task.CompletedTask;
            }

            protected override void OnBeginPlay() => trace.Add("foundation.begin");
            protected override void OnEndPlay() => trace.Add("foundation.end");
            protected override Task OnShutdownAsync()
            {
                trace.Add("foundation.shutdown");
                return Task.CompletedTask;
            }
        }

        private sealed class DependentSubSystem : WorldSubSystem
        {
            private readonly List<string> trace;

            public DependentSubSystem(List<string> trace)
            {
                this.trace = trace;
                AddDependency<FoundationSubSystem>();
            }

            protected override Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                trace.Add("dependent.initialize");
                AddTickTask("Dependent", TickGroup.TG_Gameplay, ignored => trace.Add("dependent.tick"));
                return Task.CompletedTask;
            }

            protected override void OnBeginPlay() => trace.Add("dependent.begin");
            protected override void OnEndPlay() => trace.Add("dependent.end");
            protected override Task OnShutdownAsync()
            {
                trace.Add("dependent.shutdown");
                return Task.CompletedTask;
            }
        }

        private sealed class FailingSubSystem : WorldSubSystem
        {
            private readonly List<string> trace;

            public FailingSubSystem(List<string> trace) => this.trace = trace;

            protected override Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                trace.Add("failing.initialize");
                throw new InvalidOperationException("expected");
            }
        }

        private sealed class FaultingSubSystem : WorldSubSystem
        {
            private readonly List<string> trace;

            public FaultingSubSystem(List<string> trace) => this.trace = trace;

            protected override Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                AddTickTask("Fault", TickGroup.TG_Gameplay, ignored =>
                {
                    trace.Add("tick");
                    throw new InvalidOperationException("expected");
                });
                return Task.CompletedTask;
            }

            protected override void OnBeginPlay() => trace.Add("begin");
            protected override void OnEndPlay() => trace.Add("end");
        }

        private sealed class BeginFailingSubSystem : WorldSubSystem
        {
            private readonly List<string> trace;

            public BeginFailingSubSystem(List<string> trace)
            {
                this.trace = trace;
                AddDependency<FoundationSubSystem>();
            }

            protected override void OnBeginPlay()
            {
                trace.Add("failing.begin");
                throw new InvalidOperationException("expected");
            }

            protected override void OnEndPlay() => trace.Add("failing.end");
        }

        private sealed class CancelingSubSystem : WorldSubSystem
        {
            private readonly CancellationTokenSource cancellation;
            private readonly List<string> trace;

            public CancelingSubSystem(CancellationTokenSource cancellation, List<string> trace)
            {
                this.cancellation = cancellation;
                this.trace = trace;
                AddDependency<FoundationSubSystem>();
            }

            protected override Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                trace.Add("canceling.initialize");
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class CycleASubSystem : WorldSubSystem
        {
            public CycleASubSystem() => AddDependency<CycleBSubSystem>();
        }

        private sealed class CycleBSubSystem : WorldSubSystem
        {
            public CycleBSubSystem() => AddDependency<CycleASubSystem>();
        }
    }
}
