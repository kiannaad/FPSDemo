using System;

namespace CGame
{
    public enum DiscreteActionKind
    {
        Reload,
        Melee,
        Equip,
        Unequip
    }

    public interface IDiscreteActionReplicationGateway
    {
        long BeginPredicted(DiscreteActionKind actionKind, string variantId, long equipmentInstanceId);
        void RegisterCommit(long predictionNonce, Action callback);
    }
}
