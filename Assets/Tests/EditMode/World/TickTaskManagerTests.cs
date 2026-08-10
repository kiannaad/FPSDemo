using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CGame.WorldRuntime.Tests
{
    public sealed class TickTaskManagerTests
    {
        [Test]
        public void ExecuteDomains_UseNineGroupsAndStablePrerequisiteOrder()
        {
            var manager = new TickTaskManager();
            var owner = new object();
            var trace = new List<string>();
            TickTaskNode first = Node("first", TickGroup.TG_Gameplay, trace);
            TickTaskNode second = Node("second", TickGroup.TG_Gameplay, trace);
            first.AddPrerequisite(second);
            TickTaskNode[] nodes =
            {
                Node("late", TickGroup.TG_LatePresentation, trace),
                Node("camera", TickGroup.TG_Camera, trace),
                Node("post-animation", TickGroup.TG_PostAnimation, trace),
                Node("pre-animation", TickGroup.TG_PreAnimation, trace),
                first,
                Node("input", TickGroup.TG_Input, trace),
                Node("post-physics", TickGroup.TG_PostPhysics, trace),
                Node("movement", TickGroup.TG_PhysicsMovement, trace),
                Node("pre-physics", TickGroup.TG_PrePhysics, trace),
                second
            };

            using (manager.RegisterOwner(owner, nodes))
            {
                manager.SetOwnerEnabled(owner, true);
                manager.ExecuteDomain(TickDomain.Fixed, 1f);
                manager.ExecuteDomain(TickDomain.Update, 1f);
                manager.ExecuteDomain(TickDomain.Late, 1f);
            }

            Assert.That(trace, Is.EqualTo(new[]
            {
                "pre-physics",
                "movement",
                "post-physics",
                "input",
                "second",
                "first",
                "pre-animation",
                "post-animation",
                "camera",
                "late"
            }));
            Assert.That(first.RegistrationId, Is.LessThan(second.RegistrationId));
        }

        [Test]
        public void Registration_RejectsOtherGroupOtherDomainMissingAndCyclicPrerequisites()
        {
            var manager = new TickTaskManager();
            TickTaskNode gameplay = new TickTaskNode("gameplay", TickGroup.TG_Gameplay, ignored => { });
            TickTaskNode input = new TickTaskNode("input", TickGroup.TG_Input, ignored => { });
            gameplay.AddPrerequisite(input);
            Assert.Throws<InvalidOperationException>(() => manager.RegisterOwner(new object(), new[] { gameplay, input }));

            TickTaskNode fixedNode = new TickTaskNode("fixed", TickGroup.TG_PrePhysics, ignored => { });
            TickTaskNode updateNode = new TickTaskNode("update", TickGroup.TG_Gameplay, ignored => { });
            updateNode.AddPrerequisite(fixedNode);
            Assert.Throws<InvalidOperationException>(() => manager.RegisterOwner(new object(), new[] { fixedNode, updateNode }));

            TickTaskNode missing = new TickTaskNode("missing", TickGroup.TG_Gameplay, ignored => { });
            TickTaskNode dependent = new TickTaskNode("dependent", TickGroup.TG_Gameplay, ignored => { });
            dependent.AddPrerequisite(missing);
            Assert.Throws<InvalidOperationException>(() => manager.RegisterOwner(new object(), new[] { dependent }));

            TickTaskNode cycleA = new TickTaskNode("a", TickGroup.TG_Gameplay, ignored => { });
            TickTaskNode cycleB = new TickTaskNode("b", TickGroup.TG_Gameplay, ignored => { });
            cycleA.AddPrerequisite(cycleB);
            cycleB.AddPrerequisite(cycleA);
            Assert.Throws<InvalidOperationException>(() => manager.RegisterOwner(new object(), new[] { cycleA, cycleB }));
        }

        [Test]
        public void Interval_UsesActualAccumulatedTimeOnceAndDisableResetsTimer()
        {
            var manager = new TickTaskManager();
            var owner = new object();
            var elapsed = new List<float>();
            var node = new TickTaskNode("interval", TickGroup.TG_Gameplay, elapsed.Add, 0.5f);
            using (manager.RegisterOwner(owner, new[] { node }))
            {
                manager.SetOwnerEnabled(owner, true);
                manager.ExecuteDomain(TickDomain.Update, 0.2f);
                manager.ExecuteDomain(TickDomain.Update, 0.4f);
                node.SetEnabled(false);
                manager.ExecuteDomain(TickDomain.Update, 1f);
                node.SetEnabled(true);
                manager.ExecuteDomain(TickDomain.Update, 0.3f);
                manager.ExecuteDomain(TickDomain.Update, 0.2f);
            }

            Assert.That(elapsed, Is.EqualTo(new[] { 0.6f, 0.5f }).Within(0.0001f));
        }

        [Test]
        public void NonPositiveInterval_ExecutesEveryDomainCall()
        {
            var manager = new TickTaskManager();
            var owner = new object();
            var elapsed = new List<float>();
            var node = new TickTaskNode("every-call", TickGroup.TG_Gameplay, elapsed.Add, -1f);
            using (manager.RegisterOwner(owner, new[] { node }))
            {
                manager.SetOwnerEnabled(owner, true);
                manager.ExecuteDomain(TickDomain.Update, 0.1f);
                node.SetTickInterval(0f);
                manager.ExecuteDomain(TickDomain.Update, 0.2f);
            }

            Assert.That(elapsed, Is.EqualTo(new[] { 0.1f, 0.2f }).Within(0.0001f));
        }

        [Test]
        public void OwnerFault_SkipsItsRemainingNodesButNotOtherOwnersAndReportsAfterDomain()
        {
            var manager = new TickTaskManager();
            var faultingOwner = new object();
            var healthyOwner = new object();
            var trace = new List<string>();
            TickTaskFault reported = null;
            manager.OwnerFaulted += fault =>
            {
                trace.Add("reported");
                reported = fault;
            };
            TickTaskNode throwing = new TickTaskNode(
                "throwing",
                TickGroup.TG_PostPhysics,
                ignored =>
                {
                    trace.Add("throwing");
                    throw new InvalidOperationException("expected");
                });
            TickTaskNode skipped = Node("skipped", TickGroup.TG_PreAnimation, trace);
            TickTaskNode healthy = Node("healthy", TickGroup.TG_PreAnimation, trace);

            using (manager.RegisterOwner(faultingOwner, new[] { throwing, skipped }, critical: true))
            using (manager.RegisterOwner(healthyOwner, new[] { healthy }))
            {
                manager.SetOwnerEnabled(faultingOwner, true);
                manager.SetOwnerEnabled(healthyOwner, true);
                manager.ExecuteDomain(TickDomain.Update, 1f);
            }

            Assert.That(trace, Is.EqualTo(new[] { "throwing", "healthy", "reported" }));
            Assert.That(reported, Is.Not.Null);
            Assert.That(reported.Critical, Is.True);
            Assert.That(reported.Exception.Message, Is.EqualTo("expected"));
        }

        [Test]
        public void RegistrationAndRemovalDuringExecution_AreCommittedAtDomainBoundary()
        {
            var manager = new TickTaskManager();
            var registrarOwner = new object();
            var addedOwner = new object();
            var removedOwner = new object();
            var trace = new List<string>();
            IDisposable addedRegistration = null;
            IDisposable removedRegistration = null;
            var registrar = new TickTaskNode("registrar", TickGroup.TG_PostPhysics, ignored =>
            {
                trace.Add("registrar");
                if (addedRegistration == null)
                {
                    addedRegistration = manager.RegisterOwner(
                        addedOwner,
                        new[] { Node("added", TickGroup.TG_Gameplay, trace) });
                    manager.SetOwnerEnabled(addedOwner, true);
                    removedRegistration.Dispose();
                }
            });

            using (manager.RegisterOwner(registrarOwner, new[] { registrar }))
            {
                removedRegistration = manager.RegisterOwner(
                    removedOwner,
                    new[] { Node("removed", TickGroup.TG_Gameplay, trace) });
                manager.SetOwnerEnabled(registrarOwner, true);
                manager.SetOwnerEnabled(removedOwner, true);
                manager.ExecuteDomain(TickDomain.Update, 1f);
                manager.ExecuteDomain(TickDomain.Update, 1f);
            }

            addedRegistration.Dispose();
            Assert.That(trace, Is.EqualTo(new[] { "registrar", "registrar", "added" }));
        }

        private static TickTaskNode Node(string name, TickGroup group, List<string> trace)
        {
            return new TickTaskNode(name, group, ignored => trace.Add(name));
        }
    }
}
