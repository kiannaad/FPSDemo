using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CGame.WorldRuntime.Tests
{
    public sealed class TickSchedulerTests
    {
        [Test]
        public void ExecuteDomain_UsesGroupThenPrerequisiteThenRegistrationOrder()
        {
            TickScheduler scheduler = new TickScheduler();
            List<string> trace = new List<string>();
            TickFunctionHandle first = scheduler.Register(
                "First",
                TickGroup.TG_Gameplay,
                deltaTime => trace.Add($"first:{deltaTime}"));
            TickFunctionHandle second = scheduler.Register(
                "Second",
                TickGroup.TG_Gameplay,
                deltaTime => trace.Add($"second:{deltaTime}"));
            TickFunctionHandle controller = scheduler.Register(
                "Controller",
                TickGroup.TG_Controller,
                deltaTime => trace.Add($"controller:{deltaTime}"));
            first.AddPrerequisite(second);

            scheduler.ExecuteDomain(TickDomain.Update, 0.25f);

            Assert.That(trace, Is.EqualTo(new[]
            {
                "controller:0.25",
                "second:0.25",
                "first:0.25"
            }));
            Assert.That(first.RegistrationId, Is.LessThan(second.RegistrationId));
        }

        [Test]
        public void AddPrerequisite_RejectsCyclesOtherGroupsAndOtherDomains()
        {
            TickScheduler scheduler = new TickScheduler();
            TickFunctionHandle first = scheduler.Register("First", TickGroup.TG_Gameplay, ignored => { });
            TickFunctionHandle second = scheduler.Register("Second", TickGroup.TG_Gameplay, ignored => { });
            TickFunctionHandle controller = scheduler.Register("Controller", TickGroup.TG_Controller, ignored => { });
            TickFunctionHandle fixedTick = scheduler.Register("Fixed", TickGroup.TG_PrePhysics, ignored => { });
            first.AddPrerequisite(second);

            Assert.Throws<InvalidOperationException>(() => second.AddPrerequisite(first));
            Assert.Throws<InvalidOperationException>(() => first.AddPrerequisite(controller));
            Assert.Throws<InvalidOperationException>(() => first.AddPrerequisite(fixedTick));
        }

        [Test]
        public void RuntimeRegistration_IsCommittedOnlyAtDomainBoundary()
        {
            TickScheduler scheduler = new TickScheduler();
            List<string> trace = new List<string>();
            TickFunctionHandle added = null;
            scheduler.Register("Registrar", TickGroup.TG_Gameplay, ignored =>
            {
                trace.Add("registrar");
                if (added == null)
                {
                    added = scheduler.Register("Added", TickGroup.TG_Gameplay, value => trace.Add("added"));
                }
            });

            scheduler.ExecuteDomain(TickDomain.Update, 1f);
            scheduler.ExecuteDomain(TickDomain.Update, 1f);

            Assert.That(trace, Is.EqualTo(new[] { "registrar", "registrar", "added" }));
        }

        [Test]
        public void UnregisterDuringTick_IsImmediatelyLogicalAndSkipsDependent()
        {
            TickScheduler scheduler = new TickScheduler();
            List<string> trace = new List<string>();
            TickFunctionHandle prerequisite = null;
            prerequisite = scheduler.Register("Prerequisite", TickGroup.TG_Gameplay, ignored =>
            {
                trace.Add("prerequisite");
                prerequisite.Dispose();
            });
            TickFunctionHandle dependent = scheduler.Register(
                "Dependent",
                TickGroup.TG_Gameplay,
                ignored => trace.Add("dependent"));
            dependent.AddPrerequisite(prerequisite);

            scheduler.ExecuteDomain(TickDomain.Update, 1f);

            Assert.That(trace, Is.EqualTo(new[] { "prerequisite" }));
            Assert.That(prerequisite.IsRegistered, Is.False);
        }

        [Test]
        public void Disable_IsImmediateButUnregisterIsReservedForLifecycleEnd()
        {
            TickScheduler scheduler = new TickScheduler();
            int ticks = 0;
            TickFunctionHandle handle = scheduler.Register(
                "Optional",
                TickGroup.TG_Gameplay,
                ignored => ticks++);
            handle.SetEnabled(false);

            scheduler.ExecuteDomain(TickDomain.Update, 1f);
            handle.SetEnabled(true);
            scheduler.ExecuteDomain(TickDomain.Update, 1f);

            Assert.That(ticks, Is.EqualTo(1));
            Assert.That(handle.IsRegistered, Is.True);
        }

        [Test]
        public void Fault_DisablesOnlyFaultedDependencySubgraphAndReportsCriticalOwner()
        {
            TickScheduler scheduler = new TickScheduler();
            List<string> trace = new List<string>();
            TickFault reportedFault = null;
            scheduler.Faulted += fault => reportedFault = fault;
            TickFunctionHandle faulted = scheduler.Register(
                "Critical",
                TickGroup.TG_Gameplay,
                ignored => throw new InvalidOperationException("expected"),
                critical: true);
            TickFunctionHandle dependent = scheduler.Register(
                "Dependent",
                TickGroup.TG_Gameplay,
                ignored => trace.Add("dependent"));
            dependent.AddPrerequisite(faulted);
            scheduler.Register("Unrelated", TickGroup.TG_Gameplay, ignored => trace.Add("unrelated"));

            scheduler.ExecuteDomain(TickDomain.Update, 1f);
            scheduler.ExecuteDomain(TickDomain.Update, 1f);

            Assert.That(trace, Is.EqualTo(new[] { "unrelated", "unrelated" }));
            Assert.That(faulted.IsFaulted, Is.True);
            Assert.That(reportedFault, Is.Not.Null);
            Assert.That(reportedFault.Critical, Is.True);
            Assert.That(reportedFault.Exception.Message, Is.EqualTo("expected"));
        }

        [Test]
        public void EachDomainReceivesOnlyItsProvidedDeltaTime()
        {
            TickScheduler scheduler = new TickScheduler();
            List<float> values = new List<float>();
            scheduler.Register("Fixed", TickGroup.TG_PrePhysics, values.Add);
            scheduler.Register("Update", TickGroup.TG_PostPhysics, values.Add);
            scheduler.Register("Late", TickGroup.TG_PostAnimation, values.Add);

            scheduler.ExecuteDomain(TickDomain.Fixed, 0.02f);
            scheduler.ExecuteDomain(TickDomain.Update, 0.03f);
            scheduler.ExecuteDomain(TickDomain.Late, 0.04f);

            Assert.That(values, Is.EqualTo(new[] { 0.02f, 0.03f, 0.04f }));
        }
    }
}
