using System;
using System.Collections.Generic;
using CGame.Network;

namespace CGame
{
    public interface INetworkTargetBinding
    {
        string TargetId { get; }
        bool IsDisposed { get; }
        HealthComponent Health { get; }
    }

    public sealed class NetworkTargetRegistry : IDisposable
    {
        private readonly Dictionary<string, INetworkTargetBinding> bindingsByTargetId = new Dictionary<string, INetworkTargetBinding>(StringComparer.Ordinal);
        private readonly Dictionary<string, TargetStateMessage> pendingByTargetId = new Dictionary<string, TargetStateMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> highestRevisionByTargetId = new Dictionary<string, long>(StringComparer.Ordinal);

        public void Register(EnemySpawnHandle handle)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            Register(new EnemySpawnHandleBinding(handle));
        }

        public void Register(INetworkTargetBinding binding)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (string.IsNullOrWhiteSpace(binding.TargetId)) throw new InvalidOperationException("Target binding TargetId is required.");
            if (!bindingsByTargetId.TryAdd(binding.TargetId, binding)) throw new InvalidOperationException($"TargetId is duplicated: {binding.TargetId}.");
            if (pendingByTargetId.TryGetValue(binding.TargetId, out TargetStateMessage pending))
            {
                pendingByTargetId.Remove(binding.TargetId);
                Apply(binding, pending);
            }
        }

        public void Unregister(EnemySpawnHandle handle)
        {
            if (handle != null && bindingsByTargetId.TryGetValue(handle.PointId, out INetworkTargetBinding current) &&
                current is EnemySpawnHandleBinding binding && ReferenceEquals(binding.Handle, handle))
                bindingsByTargetId.Remove(handle.PointId);
        }

        public void Apply(TargetStateMessage state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.TargetId)) return;
            if (highestRevisionByTargetId.TryGetValue(state.TargetId, out long highestRevision) && state.Revision <= highestRevision)
                return;
            highestRevisionByTargetId[state.TargetId] = state.Revision;
            if (bindingsByTargetId.TryGetValue(state.TargetId, out INetworkTargetBinding binding))
            {
                Apply(binding, state);
                return;
            }
            if (!pendingByTargetId.TryGetValue(state.TargetId, out TargetStateMessage pending) || state.Revision > pending.Revision)
                pendingByTargetId[state.TargetId] = state;
        }

        public void ApplySnapshot(TargetStateSnapshotMessage snapshot)
        {
            if (snapshot?.Targets == null) return;
            foreach (TargetStateMessage state in snapshot.Targets) Apply(state);
        }

        public void Dispose()
        {
            bindingsByTargetId.Clear();
            pendingByTargetId.Clear();
            highestRevisionByTargetId.Clear();
        }

        private static void Apply(INetworkTargetBinding binding, TargetStateMessage state)
        {
            if (binding.IsDisposed) return;
            binding.Health?.ApplyAuthoritativeState(state.Health, state.MaxHealth, state.Revision, state.IsDead);
        }

        private sealed class EnemySpawnHandleBinding : INetworkTargetBinding
        {
            public EnemySpawnHandleBinding(EnemySpawnHandle handle) { Handle = handle; }
            public EnemySpawnHandle Handle { get; }
            public string TargetId => Handle.PointId;
            public bool IsDisposed => Handle.IsDisposed;
            public HealthComponent Health => Handle.Pawn.GetComponent<HealthComponent>();
        }
    }
}
