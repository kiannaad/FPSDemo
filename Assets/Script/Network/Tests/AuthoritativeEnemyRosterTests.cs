using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CGame.Network.Tests
{
    public sealed class AuthoritativeEnemyRosterTests
    {
        [Test]
        public void Create_WhenOneEntryFails_RollsBackReservationsAndPublishesNoPartialRoster()
        {
            var factory = new RecordingFactory(failEnemyId: 503);
            var roster = new AuthoritativeEnemyRoster();

            Assert.Throws<InvalidOperationException>(() => roster.Create(Entries(), factory));

            Assert.That(roster.Entities, Is.Empty);
            Assert.That(factory.CreatedEnemyIds, Is.EqualTo(new long[] { 501, 502 }));
            Assert.That(factory.ReleasedEnemyIds, Is.EqualTo(new long[] { 502, 501 }));
            Assert.That(factory.ReleasedReservationIds, Is.EqualTo(new long[] { 503, 502, 501 }));
        }

        [Test]
        public void Create_WhenAllEntriesSucceed_PublishesStableEnemyIdsOnce()
        {
            var factory = new RecordingFactory();
            var roster = new AuthoritativeEnemyRoster();

            roster.Create(Entries(), factory);

            Assert.That(roster.Entities.Count, Is.EqualTo(3));
            Assert.That(roster.Entities[0].EnemyId, Is.EqualTo(501));
            Assert.Throws<InvalidOperationException>(() => roster.Create(Entries(), factory));
        }

        [Test]
        public void Remove_DisposesTheMatchingEntityAndReservation()
        {
            var factory = new RecordingFactory();
            using var roster = new AuthoritativeEnemyRoster();
            roster.Create(Entries(), factory);

            Assert.That(roster.Remove(502), Is.True);
            Assert.That(roster.Entities.Count, Is.EqualTo(2));
            Assert.That(factory.ReleasedEnemyIds, Does.Contain(502));
            Assert.That(factory.ReleasedReservationIds, Does.Contain(502));
            Assert.That(roster.Remove(502), Is.False);
        }

        private static EnemyRosterEntry[] Entries() => new[]
        {
            new EnemyRosterEntry(501, "Enemy.Pistol", "EnemyPoint 1"),
            new EnemyRosterEntry(502, "Enemy.Rifle", "EnemyPoint 2"),
            new EnemyRosterEntry(503, "Enemy.Ak", "EnemyPoint 3")
        };

        private sealed class RecordingFactory : IAuthoritativeEnemyRosterFactory
        {
            private readonly long failEnemyId;
            public RecordingFactory(long failEnemyId = 0) { this.failEnemyId = failEnemyId; }
            public List<long> CreatedEnemyIds { get; } = new List<long>();
            public List<long> ReleasedEnemyIds { get; } = new List<long>();
            public List<long> ReleasedReservationIds { get; } = new List<long>();

            public IAuthoritativeEnemyRosterReservation Reserve(EnemyRosterEntry entry) => new Reservation(entry.EnemyId, ReleasedReservationIds);

            public IAuthoritativeEnemyEntity Create(EnemyRosterEntry entry, IAuthoritativeEnemyRosterReservation reservation)
            {
                if (entry.EnemyId == failEnemyId) throw new InvalidOperationException("SimulatedCreateFailure");
                CreatedEnemyIds.Add(entry.EnemyId);
                return new Entity(entry.EnemyId, ReleasedEnemyIds);
            }
        }

        private sealed class Reservation : IAuthoritativeEnemyRosterReservation
        {
            private readonly List<long> released;
            public Reservation(long enemyId, List<long> released) { EnemyId = enemyId; this.released = released; }
            public long EnemyId { get; }
            public void Dispose() => released.Add(EnemyId);
        }

        private sealed class Entity : IAuthoritativeEnemyEntity
        {
            private readonly List<long> released;
            public Entity(long enemyId, List<long> released) { EnemyId = enemyId; this.released = released; }
            public long EnemyId { get; }
            public void Dispose() => released.Add(EnemyId);
        }
    }
}
