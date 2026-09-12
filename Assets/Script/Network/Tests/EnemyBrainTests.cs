using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyBrainTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void LowCover_TracksChangedTargetOnlyFromStoppedFiringPose(bool standing)
        {
            var definition = CreateDefinition("Enemy.Cover", 0);
            var point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.Low", "SampleScene", Vector3.right * 4f, Vector3.right * 5f, .3f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception,
                    new CoverReservationRegistry(), new VisibleOnlyPerceptionQuery(100));
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var original = new[] { new EnemyPerceptionCandidate(100, Vector3.right * 10f, true, true) };
                brain.Tick(181, Motor(Vector3.zero), original);
                brain.Tick(220, Motor(point.CoverPosition), original);
                if (standing) brain.Tick(280, Motor(point.CoverPosition), original);
                Vector3 changed = Vector3.right * 10f + Vector3.forward * 2f;
                var output = brain.Tick(standing ? 281 : 221, Motor(point.CoverPosition),
                    new[] { new EnemyPerceptionCandidate(100, changed, true, true) });
                Vector3 expected = standing ? (changed - point.CoverPosition).normalized : Vector3.right;
                Assert.That(Vector3.Dot(output.MovementIntent.NavigationIntent.DesiredFacing * Vector3.forward,
                    expected), Is.GreaterThan(.999f), "Hidden enemies must not acquire the player's new position through cover.");
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void LowCover_VisibleStandingTargetRemainsTrackedBeyondHiddenMemoryGrace()
        {
            var definition = CreateDefinition("Enemy.Cover", 0);
            var point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.Low", "SampleScene", Vector3.right * 4f, Vector3.right * 5f, .3f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception,
                    new CoverReservationRegistry(), new VisibleOnlyPerceptionQuery(100));
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, Vector3.right * 10f, true, true) };
                brain.Tick(181, Motor(Vector3.zero), target);
                int confirmed = 0;
                for (long tick = 220; tick <= 1000; tick++)
                {
                    var output = brain.Tick(tick, Motor(point.CoverPosition), target);
                    if (output.HasFireRequest && tick % 90 == 0)
                    {
                        brain.NotifyFireConfirmed();
                        confirmed++;
                    }
                    Assert.That(brain.CoverPointId, Is.EqualTo("Cover.Low"),
                        $"Visible standing target must refresh memory; tick={tick}");
                }
                Assert.That(confirmed, Is.GreaterThanOrEqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void LowCover_KeepsRootInPlaceForThreeConfirmedShotsBeforeCrouching()
        {
            var definition = CreateDefinition("Enemy.Cover", 0);
            var point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.Low", "SampleScene", Vector3.right * 4f, Vector3.right * 5f, .3f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception,
                    new CoverReservationRegistry(), new VisibleOnlyPerceptionQuery(100));
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, Vector3.right * 10f, true, true) };
                brain.Tick(181, Motor(Vector3.zero), target);
                brain.Tick(220, Motor(point.CoverPosition), target);
                brain.Tick(280, Motor(point.CoverPosition), target);
                brain.Tick(281, Motor(point.CoverPosition), target);
                for (int shot = 0; shot < 3; shot++)
                {
                    long tick = 293 + shot * 90;
                    var output = brain.Tick(tick, Motor(point.CoverPosition), target);
                    Assert.That(output.HasFireRequest, Is.True);
                    brain.NotifyFireConfirmed();
                    output = brain.Tick(tick + 1, Motor(point.CoverPosition), target);
                    Assert.That(output.State, Is.EqualTo(EnemyBrainState.PeekFire),
                        "Do not abandon protected firing position after a single shot.");
                    Assert.That(output.MovementIntent.NavigationIntent.HasPath, Is.False);
                    Assert.That(brain.CoverPointId, Is.EqualTo("Cover.Low"));
                }
                var lower = brain.Tick(485, Motor(point.CoverPosition), target);
                Assert.That(lower.MovementIntent.NavigationIntent.HasPath, Is.False,
                    "Shrinking behind low cover is a pose change, not a trip outside cover.");
                brain.Tick(486, Motor(point.CoverPosition), target);
                Assert.That(brain.State, Is.EqualTo(EnemyBrainState.CoverHold));
                Assert.That(brain.Tick(520, Motor(point.CoverPosition), target).State, Is.EqualTo(EnemyBrainState.CoverHold));
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Fire_WaitsForContinuousStationaryAimAndRestartsAfterMovement()
        {
            var definition = CreateDefinition("Enemy.Rifle", 0);
            try
            {
                var brain = new EnemyBrain(definition, new CompletePathQuery(), new VisibleOnlyPerceptionQuery(2));
                var target = new[] { new EnemyPerceptionCandidate(2, Vector3.right * 2f, true, true) };
                for (long tick = 181; tick < 193; tick++)
                    Assert.That(brain.Tick(tick, Motor(Vector3.zero), target).HasFireRequest, Is.False,
                        "Allow the guard-to-aim pose to settle before the first shot.");
                Assert.That(brain.Tick(193, Motor(Vector3.zero), target).HasFireRequest, Is.True);
                Assert.That(brain.Tick(194, new DedicatedEnemyMotorState(Vector3.zero,
                    Quaternion.LookRotation(Vector3.right), Vector3.right, true), target).HasFireRequest, Is.False);
                for (long tick = 195; tick < 207; tick++)
                    Assert.That(brain.Tick(tick, Motor(Vector3.zero), target).HasFireRequest, Is.False);
                Assert.That(brain.Tick(207, Motor(Vector3.zero), target).HasFireRequest, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(0.06f, 90f, true, false)]
        [TestCase(0f, 0f, true, false)]
        [TestCase(0f, 90f, false, false)]
        [TestCase(0.04f, 90f, true, true)]
        public void Fire_RequiresActualRestGroundingAndFacing(float speed, float yaw, bool grounded, bool expected)
        {
            var definition = CreateDefinition("Enemy.Rifle", 0);
            try
            {
                var brain = new EnemyBrain(definition, new CompletePathQuery(), new VisibleOnlyPerceptionQuery(2));
                var motor = new DedicatedEnemyMotorState(Vector3.zero, Quaternion.Euler(0f, yaw, 0f),
                    Vector3.forward * speed, grounded);
                brain.Tick(181, motor,
                    new[] { new EnemyPerceptionCandidate(2, Vector3.right * 2f, true, true) });
                var output = brain.Tick(193, motor,
                    new[] { new EnemyPerceptionCandidate(2, Vector3.right * 2f, true, true) });
                Assert.That(output.MovementIntent.NavigationIntent.HasPath, Is.False);
                Assert.That(output.HasFireRequest, Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(4f, 0f, 90f, false)]
        [TestCase(5f, 0.1f, 90f, false)]
        [TestCase(5f, 0f, 0f, false)]
        [TestCase(5f, 0f, 90f, true)]
        public void PeekFire_RequiresArrivalRestAndFacing(float positionX, float speed, float yaw, bool expected)
        {
            var definition = CreateDefinition("Enemy.Cover", 0);
            var point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", Vector3.right * 4f, Vector3.right * 5f, 0.3f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception, new CoverReservationRegistry());
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, Vector3.right * 10f, true, true) };
                brain.Tick(181, Motor(Vector3.zero), target);
                brain.Tick(182, Motor(Vector3.zero), target);
                brain.Tick(220, Motor(point.CoverPosition), target);
                brain.Tick(241, Motor(point.CoverPosition), target);
                brain.Tick(248, new DedicatedEnemyMotorState(Vector3.right * positionX,
                    Quaternion.Euler(0f, yaw, 0f), Vector3.forward * speed, true), target);
                var output = brain.Tick(260, new DedicatedEnemyMotorState(Vector3.right * positionX,
                    Quaternion.Euler(0f, yaw, 0f), Vector3.forward * speed, true), target);
                Assert.That(output.HasFireRequest, Is.EqualTo(expected));
                if (expected) Assert.That(output.MovementIntent.NavigationIntent.HasPath, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void FireOutput_DoesNotInferPermissionFromState()
        {
            var output = EnemyBrainOutput.ForMovement(
                new EnemyMovementIntent(default, EnemyMoveTargetKind.ChaseTarget),
                EnemyBrainState.Fire, 2);
            Assert.That(output.HasFireRequest, Is.False,
                "Fire state is not permission: arrival, actual speed and facing must be checked first.");
        }

        [Test]
        public void Reposition_CommitsUntilArrivalThenStopsAndFires()
        {
            var definition = CreateDefinition("Enemy.Rifle", 0);
            try
            {
                var brain = new EnemyBrain(definition, new CompletePathQuery(), new VisibleOnlyPerceptionQuery(2));
                var target = new[] { new EnemyPerceptionCandidate(2, Vector3.right * 4f, true, true) };
                var started = brain.Tick(181, Motor(Vector3.zero), target);
                Assert.That(started.State, Is.EqualTo(EnemyBrainState.Chase));
                Assert.That(brain.TacticalIntent, Is.EqualTo("RepositionForFire"));
                Assert.That(started.HasFireRequest, Is.False);
                var approaching = brain.Tick(182, Motor(Vector3.right * 1.8f), target);
                Assert.That(approaching.State, Is.EqualTo(EnemyBrainState.Chase),
                    "Entering maximum weapon range must not cancel the committed firing destination.");
                Assert.That(approaching.HasFireRequest, Is.False);
                Assert.That(approaching.MovementIntent.NavigationIntent.HasPath, Is.True);
                Vector3 destination = Vector3.right * (4f - definition.FireDefinition.EngagementRange * 0.7f);
                var arriving = brain.Tick(183, new DedicatedEnemyMotorState(destination,
                    Quaternion.LookRotation(Vector3.right), Vector3.right, true), target);
                Assert.That(arriving.MovementIntent.NavigationIntent.HasPath, Is.False);
                Assert.That(arriving.HasFireRequest, Is.False);
                var settled = brain.Tick(184, Motor(destination), target);
                Assert.That(settled.State, Is.EqualTo(EnemyBrainState.Fire));
                Assert.That(brain.TacticalIntent, Is.EqualTo("StationaryFire"));
                Assert.That(settled.HasFireRequest, Is.False);
                Assert.That(brain.Tick(196, Motor(destination), target).HasFireRequest, Is.True);
                Assert.That(brain.Tick(240, Motor(destination), target).MovementIntent.NavigationIntent.HasPath, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Reposition_RejectsReachableFiringPointWithBlockedSight()
        {
            var definition = CreateDefinition("Enemy.Rifle", 0);
            try
            {
                var query = new CountingPathQuery();
                var brain = new EnemyBrain(definition, query, new FiringPointPerception());
                var target = new[] { new EnemyPerceptionCandidate(2, Vector3.right * 4f, true, true) };
                var output = brain.Tick(181, Motor(Vector3.zero), target);
                Assert.That(output.MovementIntent.NavigationIntent.HasPath, Is.True);
                Assert.That(Mathf.Abs(query.LastDestination.z), Is.GreaterThan(0.1f),
                    "The direct candidate has no target sight; use a visible side candidate instead.");
                Assert.That(output.HasFireRequest, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        private sealed class FiringPointPerception : IEnemyPerceptionQuery
        {
            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate) =>
                origin.x < 2f || Mathf.Abs(origin.z) > 0.1f;
        }

        [Test]
        public void Reposition_LostTargetDiscardsOldDestinationBeforeReacquiring()
        {
            var definition = CreateDefinition("Enemy.Rifle", 0);
            try
            {
                var brain = new EnemyBrain(definition, new CompletePathQuery(), new VisibleOnlyPerceptionQuery(2));
                var target = new[] { new EnemyPerceptionCandidate(2, Vector3.right * 4f, true, true) };
                brain.Tick(181, Motor(Vector3.zero), target);
                var searching = brain.Tick(182, Motor(Vector3.right * 2.6f), System.Array.Empty<EnemyPerceptionCandidate>());
                Assert.That(searching.HasFireRequest, Is.False);
                var reacquired = brain.Tick(183, Motor(Vector3.right * 3f), target);
                Assert.That(reacquired.State, Is.EqualTo(EnemyBrainState.Fire));
                Assert.That(reacquired.MovementIntent.NavigationIntent.HasPath, Is.False,
                    "Reacquiring in a valid firing position must not run backwards to the old destination.");
                Assert.That(reacquired.HasFireRequest, Is.False);
                Assert.That(brain.Tick(195, Motor(Vector3.right * 3f), target).HasFireRequest, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void FireOutput_RejectsMovementEvenWithExplicitRequest()
        {
            var path = new EnemyNavigationIntent(Vector3.forward, Quaternion.identity, true, false, true);
            var output = EnemyBrainOutput.ForMovement(
                new EnemyMovementIntent(path, EnemyMoveTargetKind.ChaseTarget),
                EnemyBrainState.Fire, 2, true);
            Assert.That(output.HasFireRequest, Is.False, "Movement and firing must be mutually exclusive.");
        }

        [Test]
        public void Fire_WithoutUsableCover_StopsAndFacesTarget()
        {
            var definition = CreateDefinition("Enemy.Rifle", 0);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new VisibleOnlyPerceptionQuery(2);
                var selector = new EnemyCoverSelector(System.Array.Empty<CoverPointDefinition>(),
                    navigation, perception, new CoverReservationRegistry());
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(2, Vector3.right, true, true) };
                brain.Tick(181, Motor(Vector3.zero), target);
                for (long tick = 182; tick < 198; tick++)
                {
                    var output = brain.Tick(tick, Motor(Vector3.zero), target);
                    Assert.That(output.State, Is.EqualTo(EnemyBrainState.Fire));
                    Assert.That(output.MovementIntent.NavigationIntent.HasPath, Is.False);
                    Assert.That(output.MovementIntent.NavigationIntent.DesiredWorldDirection, Is.EqualTo(Vector3.zero));
                    Assert.That(output.HasFireRequest, Is.EqualTo(tick >= 193));
                    Assert.That(output.MovementIntent.NavigationIntent.HasFacing, Is.True);
                    Assert.That(Vector3.Angle(output.MovementIntent.NavigationIntent.DesiredFacing * Vector3.forward,
                        Vector3.right), Is.LessThan(0.1f));
                }
            }
            finally
            {
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CoverBrain_TransitionsThroughTakeCoverHoldAndPeekFire()
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Cover", 0);
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), 0.75f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception, new CoverReservationRegistry());
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, new Vector3(10f, 0f, 0f), true, true) };

                EnemyBrainOutput chase = brain.Tick(181, Motor(Vector3.zero), target);
                EnemyBrainOutput takeCover = brain.Tick(182, Motor(Vector3.zero), target);
                Assert.That(brain.TacticalIntent, Is.EqualTo("UseCover"));
                EnemyBrainOutput hold = brain.Tick(220, Motor(point.CoverPosition), target);
                EnemyBrainOutput peek = brain.Tick(241, Motor(point.CoverPosition), target);
                brain.Tick(248, Motor(point.PeekPosition), target);
                EnemyBrainOutput fire = brain.Tick(260, Motor(point.PeekPosition), target);

                Assert.That(chase.State, Is.EqualTo(EnemyBrainState.TakeCover),
                    "The first visible target should commit directly to available cover, not take a chase step first.");
                Assert.That(takeCover.State, Is.EqualTo(EnemyBrainState.TakeCover));
                Assert.That(hold.State, Is.EqualTo(EnemyBrainState.CoverHold));
                Assert.That(peek.State, Is.EqualTo(EnemyBrainState.PeekFire));
                Assert.That(fire.State, Is.EqualTo(EnemyBrainState.PeekFire));
                Assert.That(fire.HasFireRequest, Is.True);
                Assert.That(Vector3.Dot(fire.MovementIntent.NavigationIntent.DesiredFacing * Vector3.forward,
                    Vector3.right), Is.GreaterThan(0.99f), "Stationary firing must face the target, not the last path segment.");
                EnemyBrainOutput pendingFire = brain.Tick(261, Motor(point.PeekPosition), target);
                Assert.That(pendingFire.State, Is.EqualTo(EnemyBrainState.PeekFire),
                    "A fire request is not a confirmed shot; cooldown or facing may defer it.");
                EnemyBrainOutput timedOut = brain.Tick(410, Motor(point.PeekPosition), target);
                Assert.That(timedOut.State, Is.EqualTo(EnemyBrainState.ReturnToCover),
                    "An unconfirmed/obstructed shot must not leave the enemy exposed forever.");
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CoverBrain_PeekFireReturnsToCoverBeforeHoldingAgain()
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Cover", 0);
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), 0.75f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception, new CoverReservationRegistry());
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, new Vector3(10f, 0f, 0f), true, true) };

                brain.Tick(181, Motor(Vector3.zero), target);
                brain.Tick(182, Motor(Vector3.zero), target);
                brain.Tick(220, Motor(point.CoverPosition), target);
                brain.Tick(241, Motor(point.CoverPosition), target);
                brain.Tick(248, Motor(point.PeekPosition), target);
                EnemyBrainOutput fired = brain.Tick(260, Motor(point.PeekPosition), target);
                brain.NotifyFireConfirmed();
                EnemyBrainOutput recovering = brain.Tick(261, Motor(point.PeekPosition), target);
                Assert.That(recovering.State, Is.EqualTo(EnemyBrainState.PeekFire),
                    "Finish the shot pose before starting return travel.");
                Assert.That(recovering.HasFireRequest, Is.False);
                Assert.That(recovering.MovementIntent.NavigationIntent.HasPath, Is.False);
                EnemyBrainOutput returning = brain.Tick(272, Motor(point.PeekPosition), target);
                EnemyBrainOutput held = brain.Tick(280, Motor(point.CoverPosition), target);

                Assert.That(fired.State, Is.EqualTo(EnemyBrainState.PeekFire));
                Assert.That(fired.HasFireRequest, Is.True);
                Assert.That(returning.State, Is.EqualTo(EnemyBrainState.ReturnToCover));
                Assert.That(returning.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.CoverPosition));
                Assert.That(Vector3.Angle(returning.MovementIntent.NavigationIntent.DesiredFacing * Vector3.forward,
                    returning.MovementIntent.NavigationIntent.DesiredWorldDirection), Is.LessThan(0.01f));
                Assert.That(brain.CoverPointId, Is.EqualTo("Cover.A"));
                Assert.That(held.State, Is.EqualTo(EnemyBrainState.CoverHold));
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CoverBrain_PathFailure_ReleasesReservationAndFallsBackToChase(bool validationRemainsHealthy, bool recovers)
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Cover", 0);
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), 0.75f);
            try
            {
                var navigation = new FailsAfterFirstPathQuery();
                var perception = new CoverPerceptionQuery();
                var reservations = new CoverReservationRegistry();
                IEnemyNavPathQuery validationNavigation = validationRemainsHealthy ? new CompletePathQuery() : navigation;
                var selector = new EnemyCoverSelector(new[] { point }, validationNavigation, perception, reservations);
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, new Vector3(10f, 0f, 0f), true, true) };

                brain.Tick(181, Motor(Vector3.zero), target);
                brain.Tick(182, Motor(Vector3.zero), target);
                EnemyBrainOutput waiting = brain.Tick(183, Motor(Vector3.zero), target);
                Assert.That(brain.CoverPointId, Is.EqualTo("Cover.A"));
                Assert.That(waiting.HasFireRequest, Is.False);
                if (recovers)
                {
                    navigation.AllowRecovery = true;
                    EnemyBrainOutput recovered = brain.Tick(188, Motor(Vector3.zero), target);
                    Assert.That(recovered.MovementIntent.NavigationIntent.HasPath, Is.True);
                    Assert.That(brain.CoverPointId, Is.EqualTo("Cover.A"));
                    Assert.That(reservations.IsReservedBy("Cover.A", 101), Is.True);
                    return;
                }
                long failureDeadline = validationRemainsHealthy ? 197 : 196;
                for (long tick = 184; tick < failureDeadline; tick++)
                    brain.Tick(tick, Motor(Vector3.zero), target);
                EnemyBrainOutput fallback = brain.Tick(failureDeadline, Motor(Vector3.zero), target);

                Assert.That(fallback.State, Is.EqualTo(EnemyBrainState.Chase));
                Assert.That(fallback.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.ChaseTarget));
                Assert.That(brain.CoverPointId, Is.Empty);
                Assert.That(reservations.IsReservedBy("Cover.A", 101), Is.False);
                Assert.That(brain.LastCoverExitReason, Is.EqualTo("DestinationPathMissing"));
                Assert.That(brain.LastCoverExitTick, Is.EqualTo(failureDeadline));
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CoverBrain_TransientInvalidityRetainsCoverButPersistentFailureReleasesWithCooldown(bool peekOccluded)
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Cover", 0);
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), 0.75f);
            try
            {
                var navigation = new CountingPathQuery();
                var perception = new RevalidatingCoverPerceptionQuery(peekOccluded);
                var reservations = new CoverReservationRegistry();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception, reservations);
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, new Vector3(10f, 0f, 0f), true, true) };

                brain.Tick(181, Motor(Vector3.zero), target);
                brain.Tick(182, Motor(Vector3.zero), target);
                perception.IsInvalid = true;
                EnemyBrainOutput transient = brain.Tick(183, Motor(Vector3.zero), target);
                Assert.That(brain.CoverPointId, Is.EqualTo("Cover.A"));
                Assert.That(transient.HasFireRequest, Is.False);
                Assert.That(reservations.IsReservedBy("Cover.A", 101), Is.True);
                perception.IsInvalid = false;
                brain.Tick(184, Motor(Vector3.zero), target);
                Assert.That(brain.CoverPointId, Is.EqualTo("Cover.A"));
                perception.IsInvalid = true;
                for (long tick = 185; tick < 200; tick++)
                {
                    brain.Tick(tick, Motor(Vector3.zero), target);
                    Assert.That(brain.CoverPointId, Is.EqualTo("Cover.A"));
                }
                EnemyBrainOutput fallback = brain.Tick(200, Motor(Vector3.zero), target);
                EnemyCoverValidationFailure failure = brain.LastCoverValidationFailure;
                int coverPathRequestsAfterFailure = navigation.CoverPathRequests;
                brain.Tick(201, Motor(Vector3.zero), target);

                Assert.That(fallback.State, Is.EqualTo(EnemyBrainState.Chase));
                Assert.That(fallback.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.ChaseTarget));
                Assert.That(brain.CoverPointId, Is.Empty);
                Assert.That(failure, Is.EqualTo(peekOccluded ? EnemyCoverValidationFailure.PeekOccluded : EnemyCoverValidationFailure.CoverVisible));
                Assert.That(reservations.IsReservedBy("Cover.A", 101), Is.False);
                Assert.That(navigation.CoverPathRequests, Is.EqualTo(coverPathRequestsAfterFailure));
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        private static DedicatedEnemyMotorState Motor(Vector3 position) =>
            new DedicatedEnemyMotorState(position, Quaternion.LookRotation(Vector3.right), Vector3.zero, true);

        [TestCase(false)]
        [TestCase(true)]
        public void CoverBrain_MissingTargetRetainsStoppedCommitmentUntilGraceExpires(bool reservationLost)
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Cover", 0);
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", Vector3.right * 4f, Vector3.right * 5f, 0.75f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var reservations = new CoverReservationRegistry();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception, reservations);
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, Vector3.right * 10f, true, true) };
                brain.Tick(181, Motor(Vector3.zero), target);
                brain.Tick(182, Motor(Vector3.zero), target);
                var missing = System.Array.Empty<EnemyPerceptionCandidate>();
                if (reservationLost) reservations.ReleaseByEnemy(101);
                EnemyBrainOutput waiting = brain.Tick(183, Motor(Vector3.zero), missing);
                if (reservationLost)
                {
                    Assert.That(brain.CoverPointId, Is.Empty);
                    Assert.That(brain.LastCoverExitReason, Is.EqualTo("ReservationLost"));
                    Assert.That(brain.LastCoverExitTick, Is.EqualTo(183));
                    return;
                }
                Assert.That(waiting.State, Is.EqualTo(EnemyBrainState.TakeCover));
                Assert.That(waiting.MovementIntent.NavigationIntent.HasPath, Is.False);
                Assert.That(waiting.HasFireRequest, Is.False);
                brain.Tick(302, Motor(Vector3.zero), missing);
                Assert.That(brain.CoverPointId, Is.EqualTo("Cover.A"));
                brain.Tick(303, Motor(Vector3.zero), missing);
                Assert.That(brain.CoverPointId, Is.Empty);
                Assert.That(reservations.IsReservedBy("Cover.A", 101), Is.False);
                Assert.That(brain.LastCoverExitReason, Is.EqualTo("TargetLost"));
                Assert.That(brain.LastCoverExitTick, Is.EqualTo(303));
                Assert.That(brain.LastExitedCoverPointId, Is.EqualTo("Cover.A"));
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CoverBrain_HoldStartsOnlyAfterStoppingAndRestartsAfterDisplacement(bool neverArrives)
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Cover", 0);
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", Vector3.right * 4f, Vector3.right * 5f, 0.75f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception, new CoverReservationRegistry());
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, Vector3.right * 10f, true, true) };
                brain.Tick(181, Motor(Vector3.zero), target);
                brain.Tick(182, Motor(Vector3.zero), target);
                if (neverArrives)
                {
                    for (long tick = 183; tick <= 1380; tick++)
                        brain.Tick(tick, Motor(Vector3.zero), target);
                    Assert.That(brain.CoverPointId, Is.EqualTo("Cover.A"));
                    brain.Tick(1381, Motor(Vector3.zero), target);
                    Assert.That(brain.CoverPointId, Is.Empty);
                    Assert.That(brain.LastCoverExitReason, Is.EqualTo("TravelTimeout"));
                    return;
                }
                var arriving = new DedicatedEnemyMotorState(point.CoverPosition, Quaternion.identity, Vector3.right, true);
                Assert.That(brain.Tick(200, arriving, target).State, Is.EqualTo(EnemyBrainState.TakeCover));
                Assert.That(brain.Tick(201, Motor(point.CoverPosition), target).State, Is.EqualTo(EnemyBrainState.CoverHold));
                Assert.That(brain.Tick(210, Motor(Vector3.zero), target).State, Is.EqualTo(EnemyBrainState.TakeCover));
                Assert.That(brain.Tick(220, Motor(point.CoverPosition), target).State, Is.EqualTo(EnemyBrainState.CoverHold));
                Assert.That(brain.Tick(239, Motor(point.CoverPosition), target).State, Is.EqualTo(EnemyBrainState.CoverHold));
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        private sealed class CoverPerceptionQuery : IEnemyPerceptionQuery
        {
            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate) => origin.x != 4f;
        }
        [Test]
        public void CombatCatalog_RequiresThreeUniqueArchetypeDefinitions()
        {
            EnemyArchetypeCombatCatalog catalog = ScriptableObject.CreateInstance<EnemyArchetypeCombatCatalog>();
            EnemyArchetypeCombatDefinition[] definitions = new EnemyArchetypeCombatDefinition[3];
            try
            {
                for (int index = 0; index < definitions.Length; index++)
                    definitions[index] = CreateDefinition($"Enemy.{index}", index);

                catalog.Configure(definitions);

                Assert.That(catalog.GetRequired("Enemy.1"), Is.SameAs(definitions[1]));
                Assert.Throws<System.InvalidOperationException>(() => catalog.Configure(definitions[0], definitions[0], definitions[2]));
            }
            finally
            {
                foreach (EnemyArchetypeCombatDefinition definition in definitions)
                {
                    if (definition == null) continue;
                    Object.DestroyImmediate(definition.PatrolRoute);
                    Object.DestroyImmediate(definition);
                }
                Object.DestroyImmediate(catalog);
            }
        }

        [TestCase("Enemy.Pistol")]
        [TestCase("Enemy.Rifle")]
        [TestCase("Enemy.Ak")]
        public void Patrol_ThreeArchetypesProduceMotorIntentAndNeverFire(string archetypeId)
        {
            PatrolRouteDefinition route = ScriptableObject.CreateInstance<PatrolRouteDefinition>();
            EnemyArchetypeCombatDefinition definition = ScriptableObject.CreateInstance<EnemyArchetypeCombatDefinition>();
            try
            {
                route.Configure(
                    $"{archetypeId}.Route",
                    "SampleScene",
                    new[] { Vector3.zero, Vector3.right * 4f },
                    0.5f);
                definition.Configure(archetypeId, route);
                var brain = new EnemyBrain(definition, new CompletePathQuery());

                EnemyBrainOutput output = brain.TickPatrol(
                    30,
                    new DedicatedEnemyMotorState(Vector3.zero, Quaternion.identity, Vector3.zero, true));

                Assert.That(output.HasMovementIntent, Is.True);
                Assert.That(output.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.PatrolRoutePoint));
                Assert.That(output.HasFireRequest, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void Perception_SelectsNearestVisiblePawnThenReturnsToNearestRoutePointAfterGrace()
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Rifle", 0);
            try
            {
                var brain = new EnemyBrain(definition, new CompletePathQuery(), new VisibleOnlyPerceptionQuery(2));
                var motorState = new DedicatedEnemyMotorState(
                    Vector3.zero, Quaternion.identity, Vector3.zero, true);
                var candidates = new[]
                {
                    new EnemyPerceptionCandidate(7, new Vector3(4f, 0f, 0f), true, true),
                    new EnemyPerceptionCandidate(2, new Vector3(4f, 0f, 0f), true, true),
                    new EnemyPerceptionCandidate(1, new Vector3(1f, 0f, 0f), false, true)
                };

                EnemyBrainOutput chase = brain.Tick(181, motorState, candidates);

                Assert.That(chase.State, Is.EqualTo(EnemyBrainState.Chase));
                Assert.That(chase.TargetPawnId, Is.EqualTo(2));
                Assert.That(chase.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.ChaseTarget));

                EnemyBrainOutput grace = brain.Tick(182, motorState, System.Array.Empty<EnemyPerceptionCandidate>());
                Assert.That(grace.State, Is.EqualTo(EnemyBrainState.Chase));

                var returnMotorState = new DedicatedEnemyMotorState(
                    new Vector3(2.6f, 0f, 0f), Quaternion.identity, Vector3.zero, true);
                EnemyBrainOutput returning = brain.Tick(302, returnMotorState, System.Array.Empty<EnemyPerceptionCandidate>());
                Assert.That(returning.State, Is.EqualTo(EnemyBrainState.ReturnToRoute));
                Assert.That(returning.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.ReturnRoutePoint));
                Assert.That(returning.TargetPawnId, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        private sealed class CompletePathQuery : IEnemyNavPathQuery
        {
            public bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint)
            {
                sampledPoint = worldPoint;
                return true;
            }

            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                corners = new[] { origin, destination };
                return true;
            }
        }

        private sealed class FailsAfterFirstPathQuery : IEnemyNavPathQuery
        {
            private int pathRequests;
            public bool AllowRecovery { get; set; }

            public bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint)
            {
                sampledPoint = worldPoint;
                return true;
            }

            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                if (Mathf.Approximately(destination.x, 4f)) pathRequests++;
                bool complete = AllowRecovery || pathRequests < 2;
                corners = complete ? new[] { origin, destination } : System.Array.Empty<Vector3>();
                return complete;
            }
        }

        private sealed class CountingPathQuery : IEnemyNavPathQuery
        {
            public Vector3 LastDestination { get; private set; }
            public int PathRequests { get; private set; }
            public int CoverPathRequests { get; private set; }

            public bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint)
            {
                sampledPoint = worldPoint;
                return true;
            }

            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                PathRequests++;
                LastDestination = destination;
                if (Mathf.Approximately(destination.x, 4f)) CoverPathRequests++;
                corners = new[] { origin, destination };
                return true;
            }
        }

        private sealed class RevalidatingCoverPerceptionQuery : IEnemyPerceptionQuery
        {
            private readonly bool failAtPeek;
            public RevalidatingCoverPerceptionQuery(bool failAtPeek) => this.failAtPeek = failAtPeek;
            public bool IsInvalid { get; set; }

            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate)
            {
                if (Mathf.Approximately(origin.x, 4f)) return !failAtPeek && IsInvalid;
                if (Mathf.Approximately(origin.x, 5f)) return !failAtPeek || !IsInvalid;
                return true;
            }
        }

        private sealed class VisibleOnlyPerceptionQuery : IEnemyPerceptionQuery
        {
            private readonly long visiblePawnId;

            public VisibleOnlyPerceptionQuery(long visiblePawnId)
            {
                this.visiblePawnId = visiblePawnId;
            }

            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate) => candidate.PawnId == visiblePawnId;
        }

        private static EnemyArchetypeCombatDefinition CreateDefinition(string archetypeId, int index)
        {
            PatrolRouteDefinition route = ScriptableObject.CreateInstance<PatrolRouteDefinition>();
            route.Configure($"{archetypeId}.Route", "SampleScene", new[]
            {
                new Vector3(index, 0f, 0f),
                new Vector3(index + 1f, 0f, 0f)
            }, 0.5f);
            EnemyArchetypeCombatDefinition definition = ScriptableObject.CreateInstance<EnemyArchetypeCombatDefinition>();
            definition.Configure(archetypeId, route);
            if (archetypeId == "Enemy.Cover")
                definition.Configure(archetypeId, route, new EnemyFireDefinition(12f, 18f, 0.8f, 30, 30, 6));
            return definition;
        }
    }
}
