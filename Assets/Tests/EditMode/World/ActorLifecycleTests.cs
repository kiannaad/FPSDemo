using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CGame.WorldRuntime.Tests
{
    public sealed class ActorLifecycleTests
    {
        [Test]
        public void RegistrationAndActivation_UseDependencyOrderAndKeepTickClosedUntilPlaying()
        {
            var trace = new List<string>();
            var manager = new TickTaskManager();
            var actor = new TraceActor(trace);

            using (ActorRegistration registration = ActorRegistration.Register(actor, manager))
            {
                Assert.That(actor.State, Is.EqualTo(ActorState.Initialized));
                Assert.That(trace, Is.EqualTo(new[] { "actor.initialize", "first.initialize", "second.initialize", "actor.post" }));

                manager.ExecuteDomain(TickDomain.Update, 0.25f);
                CollectionAssert.DoesNotContain(trace, "second.tick:0.25");

                registration.Activate();
                manager.ExecuteDomain(TickDomain.Update, 0.25f);

                Assert.That(actor.State, Is.EqualTo(ActorState.Playing));
                Assert.That(trace, Is.EqualTo(new[]
                {
                    "actor.initialize",
                    "first.initialize",
                    "second.initialize",
                    "actor.post",
                    "first.begin",
                    "second.begin",
                    "actor.begin",
                    "second.tick:0.25"
                }));
            }

            Assert.That(actor.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(trace[trace.Count - 1], Is.EqualTo("first.shutdown"));
        }

        [Test]
        public void Register_RejectsMissingAndCyclicComponentDependencies()
        {
            var manager = new TickTaskManager();
            var missing = new MissingDependencyActor();
            var cyclic = new CyclicDependencyActor();

            InvalidOperationException missingException = Assert.Throws<InvalidOperationException>(
                () => ActorRegistration.Register(missing, manager));
            InvalidOperationException cycleException = Assert.Throws<InvalidOperationException>(
                () => ActorRegistration.Register(cyclic, manager));

            Assert.That(missingException.Message, Does.Contain("missing"));
            Assert.That(cycleException.Message, Does.Contain("cycle"));
            Assert.That(missing.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(cyclic.State, Is.EqualTo(ActorState.Unregistered));
        }

        [Test]
        public void InitializeFailure_ShutsDownOnlyInitializedComponentsInReverseOrder()
        {
            var trace = new List<string>();
            var actor = new InitializationFailureActor(trace);

            Assert.Throws<InvalidOperationException>(
                () => ActorRegistration.Register(actor, new TickTaskManager()));

            Assert.That(trace, Is.EqualTo(new[]
            {
                "first.initialize",
                "failing.initialize",
                "first.shutdown"
            }));
            Assert.That(actor.State, Is.EqualTo(ActorState.Unregistered));
        }

        [Test]
        public void BeginPlayFailure_DoesNotEndFailingComponentAndRollsBackEarlierComponent()
        {
            var trace = new List<string>();
            var actor = new BeginFailureActor(trace);
            ActorRegistration registration = ActorRegistration.Register(actor, new TickTaskManager());

            Assert.Throws<InvalidOperationException>(() => registration.Activate());

            Assert.That(trace, Is.EqualTo(new[]
            {
                "first.initialize",
                "failing.initialize",
                "first.begin",
                "failing.begin",
                "first.end",
                "failing.shutdown",
                "first.shutdown"
            }));
            Assert.That(actor.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(registration.IsDisposed, Is.True);
        }

        [Test]
        public void ActorStructure_CannotChangeOutsideInitializeAndRegistrationIsOneShot()
        {
            var actor = new MutationActor();
            var manager = new TickTaskManager();
            using (ActorRegistration registration = ActorRegistration.Register(actor, manager))
            {
                Assert.Throws<InvalidOperationException>(() => actor.AddLateComponent());
                Assert.Throws<InvalidOperationException>(() => ActorRegistration.Register(actor, manager));
                registration.Activate();
                Assert.Throws<InvalidOperationException>(() => registration.Activate());
            }
        }

        private sealed class TraceActor : Actor
        {
            private readonly List<string> trace;

            public TraceActor(List<string> trace)
            {
                this.trace = trace;
            }

            protected override void OnInitialize()
            {
                trace.Add("actor.initialize");
                AddComponent(new SecondComponent(trace));
                AddComponent(new FirstComponent(trace));
            }

            protected override void OnPostInitializeComponents() => trace.Add("actor.post");

            protected override void OnBeginPlay() => trace.Add("actor.begin");

            protected override void OnEndPlay() => trace.Add("actor.end");
        }

        private class FirstComponent : ActorComponent
        {
            protected readonly List<string> Trace;

            public FirstComponent(List<string> trace)
            {
                Trace = trace;
            }

            protected override void OnInitialize() => Trace.Add("first.initialize");

            protected override void OnBeginPlay() => Trace.Add("first.begin");

            protected override void OnEndPlay() => Trace.Add("first.end");

            protected override void OnShutdown() => Trace.Add("first.shutdown");
        }

        private sealed class SecondComponent : ActorComponent
        {
            private readonly List<string> trace;

            public SecondComponent(List<string> trace)
            {
                this.trace = trace;
                AddDependency<FirstComponent>();
            }

            protected override void OnInitialize()
            {
                trace.Add("second.initialize");
                AddTickTask("Second", TickGroup.TG_Gameplay, value => trace.Add($"second.tick:{value}"));
            }

            protected override void OnBeginPlay() => trace.Add("second.begin");

            protected override void OnEndPlay() => trace.Add("second.end");

            protected override void OnShutdown() => trace.Add("second.shutdown");
        }

        private sealed class MissingDependencyActor : Actor
        {
            protected override void OnInitialize() => AddComponent(new SecondComponent(new List<string>()));
        }

        private sealed class CyclicDependencyActor : Actor
        {
            protected override void OnInitialize()
            {
                AddComponent(new CycleA());
                AddComponent(new CycleB());
            }
        }

        private sealed class CycleA : ActorComponent
        {
            public CycleA() => AddDependency<CycleB>();
        }

        private sealed class CycleB : ActorComponent
        {
            public CycleB() => AddDependency<CycleA>();
        }

        private sealed class InitializationFailureActor : Actor
        {
            private readonly List<string> trace;

            public InitializationFailureActor(List<string> trace)
            {
                this.trace = trace;
            }

            protected override void OnInitialize()
            {
                AddComponent(new FirstComponent(trace));
                AddComponent(new InitializationFailureComponent(trace));
            }
        }

        private sealed class InitializationFailureComponent : ActorComponent
        {
            private readonly List<string> trace;

            public InitializationFailureComponent(List<string> trace)
            {
                this.trace = trace;
                AddDependency<FirstComponent>();
            }

            protected override void OnInitialize()
            {
                trace.Add("failing.initialize");
                throw new InvalidOperationException("expected");
            }

            protected override void OnShutdown() => trace.Add("failing.shutdown");
        }

        private sealed class BeginFailureActor : Actor
        {
            private readonly List<string> trace;

            public BeginFailureActor(List<string> trace)
            {
                this.trace = trace;
            }

            protected override void OnInitialize()
            {
                AddComponent(new FirstComponent(trace));
                AddComponent(new BeginFailureComponent(trace));
            }
        }

        private sealed class BeginFailureComponent : ActorComponent
        {
            private readonly List<string> trace;

            public BeginFailureComponent(List<string> trace)
            {
                this.trace = trace;
                AddDependency<FirstComponent>();
            }

            protected override void OnInitialize() => trace.Add("failing.initialize");

            protected override void OnBeginPlay()
            {
                trace.Add("failing.begin");
                throw new InvalidOperationException("expected");
            }

            protected override void OnEndPlay() => trace.Add("failing.end");

            protected override void OnShutdown() => trace.Add("failing.shutdown");
        }

        private sealed class MutationActor : Actor
        {
            protected override void OnInitialize() => AddComponent(new EmptyComponent());

            public void AddLateComponent() => AddComponent(new AnotherEmptyComponent());
        }

        private sealed class EmptyComponent : ActorComponent
        {
        }

        private sealed class AnotherEmptyComponent : ActorComponent
        {
        }
    }
}
