using NUnit.Framework;

namespace CGame.Network.Tests
{
    public sealed class EnemyReplicationTrackerTests
    {
        [Test]
        public void SnapshotAndAction_RejectOldOrderingValues_AndKeepNewestState()
        {
            var tracker = new EnemyReplicationTracker();
            tracker.ApplySpawn(Spawn(501));

            Assert.That(tracker.ApplySnapshot(Snapshot(501, 10)), Is.True);
            Assert.That(tracker.ApplySnapshot(Snapshot(501, 9)), Is.False);
            Assert.That(tracker.ApplyAction(Action(501, 4)), Is.True);
            Assert.That(tracker.ApplyAction(Action(501, 4)), Is.False);

            Assert.That(tracker.TryGet(501, out EnemyReplicationState state), Is.True);
            Assert.That(state.LatestAuthorityServerTick, Is.EqualTo(10));
            Assert.That(state.LatestActionSequence, Is.EqualTo(4));
        }

        [Test]
        public void Snapshot_LatestTickOwnsCoverPointAndBrainState()
        {
            var tracker = new EnemyReplicationTracker();
            tracker.ApplySpawn(Spawn(501));
            EnemySnapshotEvent older = Snapshot(501, 10);
            older.CoverPointId = "Cover.A";
            older.BrainState = EnemyBrainState.CoverHold;
            EnemySnapshotEvent newer = Snapshot(501, 11);
            newer.CoverPointId = "Cover.B";
            newer.BrainState = EnemyBrainState.PeekFire;

            Assert.That(tracker.ApplySnapshot(older), Is.True);
            Assert.That(tracker.ApplySnapshot(newer), Is.True);
            Assert.That(tracker.TryGet(501, out EnemyReplicationState state), Is.True);
            Assert.That(state.LatestSnapshot.CoverPointId, Is.EqualTo("Cover.B"));
            Assert.That(state.LatestSnapshot.BrainState, Is.EqualTo(EnemyBrainState.PeekFire));
        }

        [Test]
        public void UnknownEntity_IsBufferedUntilSpawn_ThenAppliesNewestValues()
        {
            var tracker = new EnemyReplicationTracker();

            Assert.That(tracker.ApplySnapshot(Snapshot(501, 8), 1f), Is.False);
            Assert.That(tracker.ApplySnapshot(Snapshot(501, 10), 1.1f), Is.False);
            Assert.That(tracker.ApplyAction(Action(501, 3), 1.2f), Is.False);

            Assert.That(tracker.ApplySpawn(Spawn(501)), Is.True);
            Assert.That(tracker.TryGet(501, out EnemyReplicationState state), Is.True);
            Assert.That(state.LatestAuthorityServerTick, Is.EqualTo(10));
            Assert.That(state.LatestActionSequence, Is.EqualTo(3));
        }

        [Test]
        public void UnknownEntity_AfterTwoSeconds_RequestsResyncOnlyOnce()
        {
            var tracker = new EnemyReplicationTracker();
            tracker.ApplySnapshot(Snapshot(501, 8), 10f);

            Assert.That(tracker.CollectExpiredResyncRequests(11.99f), Is.Empty);
            Assert.That(tracker.CollectExpiredResyncRequests(12f), Is.EqualTo(new[] { new EnemyResyncRequest { EnemyId = 501, LastKnownAuthorityServerTick = 8 } }));
            Assert.That(tracker.CollectExpiredResyncRequests(15f), Is.Empty);
        }

        private static EnemySpawnedEvent Spawn(long enemyId) => new EnemySpawnedEvent
        {
            EnemyId = enemyId,
            ArchetypeId = "Enemy.Rifle",
            Position = new QuantizedVector3WireMessage(),
            Rotation = QuantizedQuaternionWireMessage.FromValue(new QuantizedQuaternion(0, 0, 0, short.MaxValue)),
            AuthorityServerTick = 1,
            Health = 100
        };

        private static EnemySnapshotEvent Snapshot(long enemyId, long tick) => new EnemySnapshotEvent
        {
            EnemyId = enemyId,
            AuthorityServerTick = tick,
            Position = new QuantizedVector3WireMessage { XMillimeters = (int)tick },
            Rotation = QuantizedQuaternionWireMessage.FromValue(new QuantizedQuaternion(0, 0, 0, short.MaxValue)),
            PlanarVelocity = new QuantizedVector3WireMessage(),
            PoseDiscontinuitySequence = 1,
            Health = 100
        };

        private static EnemyActionEvent Action(long enemyId, long sequence) => new EnemyActionEvent
        {
            EnemyId = enemyId,
            ActionSequence = sequence,
            ActionKind = EnemyActionKind.Fire,
            AuthorityServerTick = 10,
            PoseDiscontinuitySequence = 1
        };
    }
}
