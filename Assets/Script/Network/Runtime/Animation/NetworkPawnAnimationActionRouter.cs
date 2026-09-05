using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public sealed class NetworkPawnAnimationActionRouter
    {
        private readonly Dictionary<long, NetworkAnimationActionBridge> remoteBridgesByPawnId = new Dictionary<long, NetworkAnimationActionBridge>();
        private NetworkPawnBinding ownerBinding;
        private NetworkAnimationActionBridge ownerBridge;

        public void SetOwner(NetworkPawnBinding binding, NetworkAnimationActionBridge bridge)
        {
            ownerBinding = binding ?? throw new ArgumentNullException(nameof(binding));
            ownerBridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public void AddRemote(long pawnId, NetworkAnimationActionBridge bridge)
        {
            if (pawnId <= 0) throw new ArgumentOutOfRangeException(nameof(pawnId));
            remoteBridgesByPawnId.Add(pawnId, bridge ?? throw new ArgumentNullException(nameof(bridge)));
        }

        public bool ApplyStarted(NetworkAnimationActionStarted action)
        {
            if (action == null) return false;
            if (ownerBinding != null && action.PawnId == ownerBinding.PawnId)
                return ownerBridge.ApplyStarted(action);
            return remoteBridgesByPawnId.TryGetValue(action.PawnId, out NetworkAnimationActionBridge bridge) && bridge.ApplyStarted(action);
        }

        public bool ApplyTerminal(NetworkAnimationActionTerminal terminal)
        {
            if (terminal == null) return false;
            if (ownerBinding != null && terminal.PawnId == ownerBinding.PawnId)
                return ownerBridge.ApplyTerminal(terminal);
            return remoteBridgesByPawnId.TryGetValue(terminal.PawnId, out NetworkAnimationActionBridge bridge) && bridge.ApplyTerminal(terminal);
        }

        public void Clear()
        {
            ownerBinding = null;
            ownerBridge = null;
            remoteBridgesByPawnId.Clear();
        }
    }
}
