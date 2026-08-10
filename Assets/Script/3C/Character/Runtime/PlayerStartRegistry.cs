using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class PlayerStartRegistry
    {
        private readonly List<PlayerStartInfo> starts = new List<PlayerStartInfo>();
        private readonly HashSet<string> reservedIds = new HashSet<string>();

        public void Register(PlayerStartInfo start)
        {
            if (starts.Exists(candidate => candidate.Id == start.Id))
            {
                throw new InvalidOperationException($"PlayerStart {start.Id} is already registered.");
            }

            starts.Add(start);
        }

        public PlayerStartReservation ReserveFirstAvailable()
        {
            for (int index = 0; index < starts.Count; index++)
            {
                if (reservedIds.Add(starts[index].Id))
                {
                    return new PlayerStartReservation(this, starts[index]);
                }
            }

            throw new InvalidOperationException("No available PlayerStart exists.");
        }

        internal void Release(string id)
        {
            reservedIds.Remove(id);
        }
    }
}
