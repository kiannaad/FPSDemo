using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public readonly struct EnemyRosterEntry
    {
        public EnemyRosterEntry(long enemyId, string archetypeId, string spawnPointId)
        {
            if (enemyId <= 0) throw new ArgumentOutOfRangeException(nameof(enemyId));
            if (string.IsNullOrWhiteSpace(archetypeId)) throw new ArgumentException("ArchetypeId is required.", nameof(archetypeId));
            if (string.IsNullOrWhiteSpace(spawnPointId)) throw new ArgumentException("SpawnPointId is required.", nameof(spawnPointId));
            EnemyId = enemyId;
            ArchetypeId = archetypeId;
            SpawnPointId = spawnPointId;
        }

        public long EnemyId { get; }
        public string ArchetypeId { get; }
        public string SpawnPointId { get; }
    }

    public interface IAuthoritativeEnemyRosterReservation : IDisposable
    {
        long EnemyId { get; }
    }

    public interface IAuthoritativeEnemyEntity : IDisposable
    {
        long EnemyId { get; }
    }

    public interface IAuthoritativeEnemyRosterFactory
    {
        IAuthoritativeEnemyRosterReservation Reserve(EnemyRosterEntry entry);
        IAuthoritativeEnemyEntity Create(EnemyRosterEntry entry, IAuthoritativeEnemyRosterReservation reservation);
    }

    public sealed class AuthoritativeEnemyRoster : IDisposable
    {
        private readonly List<IAuthoritativeEnemyEntity> entities = new List<IAuthoritativeEnemyEntity>();
        private readonly List<IAuthoritativeEnemyRosterReservation> reservations = new List<IAuthoritativeEnemyRosterReservation>();
        private bool disposed;

        public IReadOnlyList<IAuthoritativeEnemyEntity> Entities => entities;

        public void Create(IReadOnlyList<EnemyRosterEntry> entries, IAuthoritativeEnemyRosterFactory factory)
        {
            if (disposed) throw new ObjectDisposedException(nameof(AuthoritativeEnemyRoster));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (entities.Count != 0) throw new InvalidOperationException("Authoritative enemy roster can only be created once.");
            if (entries.Count != 3) throw new InvalidOperationException("Authoritative enemy roster requires exactly three entries.");

            var enemyIds = new HashSet<long>();
            var spawnPointIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                EnemyRosterEntry entry = entries[index];
                if (!enemyIds.Add(entry.EnemyId)) throw new InvalidOperationException($"EnemyId is duplicated: {entry.EnemyId}.");
                if (!spawnPointIds.Add(entry.SpawnPointId)) throw new InvalidOperationException($"Enemy SpawnPointId is duplicated: {entry.SpawnPointId}.");
            }

            var created = new List<IAuthoritativeEnemyEntity>(entries.Count);
            var reserved = new List<IAuthoritativeEnemyRosterReservation>(entries.Count);
            try
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    EnemyRosterEntry entry = entries[index];
                    IAuthoritativeEnemyRosterReservation reservation = factory.Reserve(entry) ??
                        throw new InvalidOperationException($"Enemy reservation was not created: {entry.EnemyId}.");
                    if (reservation.EnemyId != entry.EnemyId)
                        throw new InvalidOperationException($"Enemy reservation identity mismatch: {entry.EnemyId}.");
                    reserved.Add(reservation);

                    IAuthoritativeEnemyEntity entity = factory.Create(entry, reservation) ??
                        throw new InvalidOperationException($"Enemy entity was not created: {entry.EnemyId}.");
                    if (entity.EnemyId != entry.EnemyId)
                        throw new InvalidOperationException($"Enemy entity identity mismatch: {entry.EnemyId}.");
                    created.Add(entity);
                }

                entities.AddRange(created);
                reservations.AddRange(reserved);
            }
            catch
            {
                DisposeReverse(created);
                DisposeReverse(reserved);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DisposeReverse(entities);
            DisposeReverse(reservations);
            entities.Clear();
            reservations.Clear();
        }

        public bool Remove(long enemyId)
        {
            if (disposed) throw new ObjectDisposedException(nameof(AuthoritativeEnemyRoster));
            for (int index = 0; index < entities.Count; index++)
            {
                if (entities[index].EnemyId != enemyId) continue;
                entities[index].Dispose();
                entities.RemoveAt(index);
                reservations[index].Dispose();
                reservations.RemoveAt(index);
                return true;
            }
            return false;
        }

        private static void DisposeReverse<T>(IReadOnlyList<T> values) where T : IDisposable
        {
            for (int index = values.Count - 1; index >= 0; index--) values[index].Dispose();
        }
    }
}
