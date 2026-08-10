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
        public void StartAsync_InitializesServicesAndPublishesRequest()
        {
            List<string> trace = new List<string>();
            World world = CreateWorld(
                new IWorldCoreService[]
                {
                    new ProbeService("Input", trace),
                    new ProbeService("Resource", trace)
                });

            WorldStartResult result = world.StartAsync().GetAwaiter().GetResult();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(world.State, Is.EqualTo(WorldState.Launching));
            Assert.That(world.PendingGameStartRequest.HasValue, Is.True);
            Assert.That(trace, Is.EqualTo(new[] { "init:Input", "init:Resource" }));
        }

        [Test]
        public void Create_RejectsASecondActiveWorld()
        {
            _ = CreateWorld(Array.Empty<IWorldCoreService>());

            Assert.Throws<InvalidOperationException>(() => CreateWorld(Array.Empty<IWorldCoreService>()));
        }

        [Test]
        public void CoreServiceFailure_RollsBackInitializedServicesInReverse()
        {
            List<string> trace = new List<string>();
            World world = CreateWorld(
                new IWorldCoreService[]
                {
                    new ProbeService("First", trace),
                    new ProbeService("Second", trace, failInitialization: true),
                    new ProbeService("Never", trace)
                });

            WorldStartResult result = world.StartAsync().GetAwaiter().GetResult();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(world.State, Is.EqualTo(WorldState.InitializationFailed));
            Assert.That(trace, Is.EqualTo(new[]
            {
                "init:First",
                "init:Second",
                "shutdown:First"
            }));
        }

        [Test]
        public void LaunchFailure_IsTerminalAndDoesNotRetry()
        {
            int executions = 0;
            GameLauncher launcher = new GameLauncher(new Func<ILaunchStep>[]
            {
                () => new DelegateStep(() =>
                {
                    executions++;
                    return LaunchStepResult.Fail("expected");
                })
            });
            World world = World.Create(Array.Empty<IWorldCoreService>(), launcher);

            WorldStartResult result = world.StartAsync().GetAwaiter().GetResult();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(world.State, Is.EqualTo(WorldState.LaunchFailed));
            Assert.That(executions, Is.EqualTo(1));
            bool rejected = false;
            try
            {
                world.StartAsync().GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Assert.That(rejected, Is.True);
            Assert.That(executions, Is.EqualTo(1));
        }

        [Test]
        public void ReturnToLogin_CreatesANewNormalAttempt()
        {
            World world = CreateWorld(Array.Empty<IWorldCoreService>());
            WorldStartResult first = world.StartAsync().GetAwaiter().GetResult();

            WorldStartResult second = world.ReturnToLoginAsync().GetAwaiter().GetResult();

            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Request.LaunchAttemptId.Value,
                Is.GreaterThan(first.Request.LaunchAttemptId.Value));
            Assert.That(world.State, Is.EqualTo(WorldState.Launching));
        }

        [Test]
        public void ShutdownAsync_CleansInReverseClearsCurrentAndIsIdempotent()
        {
            List<string> trace = new List<string>();
            World world = CreateWorld(
                new IWorldCoreService[]
                {
                    new ProbeService("First", trace),
                    new ProbeService("Second", trace)
                });
            world.StartAsync().GetAwaiter().GetResult();

            world.ShutdownAsync().GetAwaiter().GetResult();
            world.ShutdownAsync().GetAwaiter().GetResult();

            Assert.That(world.State, Is.EqualTo(WorldState.Destroyed));
            Assert.That(World.Current, Is.Null);
            Assert.That(trace, Is.EqualTo(new[]
            {
                "init:First",
                "init:Second",
                "shutdown:Second",
                "shutdown:First"
            }));
        }

        [Test]
        public void TickDomains_PlaceMotorStepEventsAndPresentationAtWorldBoundaries()
        {
            List<string> trace = new List<string>();
            ProbeMotorSimulation simulation = new ProbeMotorSimulation(trace);
            TickScheduler scheduler = new TickScheduler();
            scheduler.Register("PrePhysics", TickGroup.TG_PrePhysics, ignored => trace.Add("pre"));
            scheduler.Register("Input", TickGroup.TG_Input, ignored => trace.Add("input"));
            scheduler.Register("Gameplay", TickGroup.TG_Gameplay, ignored => trace.Add("gameplay"));
            scheduler.Register("PostAnimation", TickGroup.TG_PostAnimation, ignored => trace.Add("post-animation"));
            scheduler.Register("Camera", TickGroup.TG_LatePresentation, ignored => trace.Add("camera"));
            World world = World.Create(
                Array.Empty<IWorldCoreService>(),
                new GameLauncher(Array.Empty<Func<ILaunchStep>>()),
                scheduler,
                simulation);
            world.StartAsync().GetAwaiter().GetResult();

            world.FixedTick(0.02f);
            world.UpdateTick(0.03f);
            world.LateTick(0.04f, 12f);

            Assert.That(trace, Is.EqualTo(new[]
            {
                "pre",
                "motor:0.02",
                "input",
                "events:0.03",
                "gameplay",
                "post-animation",
                "present:12",
                "camera"
            }));
        }

        private static World CreateWorld(IEnumerable<IWorldCoreService> services)
        {
            return World.Create(
                services,
                new GameLauncher(new Func<ILaunchStep>[] { () => new DelegateStep(LaunchStepResult.Success) }));
        }

        private sealed class ProbeService : IWorldCoreService
        {
            private readonly List<string> trace;
            private readonly bool failInitialization;

            public ProbeService(string name, List<string> trace, bool failInitialization = false)
            {
                Name = name;
                this.trace = trace;
                this.failInitialization = failInitialization;
            }

            public string Name { get; }

            public Task InitializeAsync(CancellationToken cancellationToken)
            {
                trace.Add($"init:{Name}");
                if (failInitialization)
                {
                    throw new InvalidOperationException("expected");
                }

                return Task.CompletedTask;
            }

            public Task ShutdownAsync()
            {
                trace.Add($"shutdown:{Name}");
                return Task.CompletedTask;
            }
        }

        private sealed class DelegateStep : ILaunchStep
        {
            private readonly Func<LaunchStepResult> execute;

            public DelegateStep(Func<LaunchStepResult> execute)
            {
                this.execute = execute;
            }

            public string Name => "Test";

            public Task<LaunchStepResult> ExecuteAsync(LaunchContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(execute());
            }

            public Task ExitAsync(LaunchContext context)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class ProbeMotorSimulation : ICharacterMotorSimulation
        {
            private readonly List<string> trace;

            public ProbeMotorSimulation(List<string> trace)
            {
                this.trace = trace;
            }

            public void Step(float deltaTime) => trace.Add($"motor:{deltaTime}");

            public void ConsumePostPhysicsEvents(float deltaTime) => trace.Add($"events:{deltaTime}");

            public void Present(float currentTime) => trace.Add($"present:{currentTime}");

            public void Dispose()
            {
            }
        }
    }
}
