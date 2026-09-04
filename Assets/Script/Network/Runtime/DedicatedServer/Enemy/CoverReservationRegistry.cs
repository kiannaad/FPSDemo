using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public interface ICoverReservationRegistry
    {
        bool TryReserve(string coverPointId, long enemyId);
        void ReleaseByEnemy(long enemyId);
        bool IsReservedBy(string coverPointId, long enemyId);
    }

    public sealed class CoverReservationRegistry : ICoverReservationRegistry
    {
        private readonly Dictionary<string, long> ownerByCoverPointId = new Dictionary<string, long>(StringComparer.Ordinal);

        public bool TryReserve(string coverPointId, long enemyId)
        {
            if (string.IsNullOrWhiteSpace(coverPointId)) throw new ArgumentException("CoverPointId is required.", nameof(coverPointId));
            if (enemyId <= 0) throw new ArgumentOutOfRangeException(nameof(enemyId));
            if (ownerByCoverPointId.TryGetValue(coverPointId, out long existingOwner)) return existingOwner == enemyId;
            ownerByCoverPointId.Add(coverPointId, enemyId);
            return true;
        }

        public void ReleaseByEnemy(long enemyId)
        {
            if (enemyId <= 0) return;
            var releasedIds = new List<string>();
            foreach (KeyValuePair<string, long> entry in ownerByCoverPointId)
                if (entry.Value == enemyId) releasedIds.Add(entry.Key);
            foreach (string coverPointId in releasedIds) ownerByCoverPointId.Remove(coverPointId);
        }

        public bool IsReservedBy(string coverPointId, long enemyId) =>
            !string.IsNullOrWhiteSpace(coverPointId) && enemyId > 0 &&
            ownerByCoverPointId.TryGetValue(coverPointId, out long owner) && owner == enemyId;
    }
}
