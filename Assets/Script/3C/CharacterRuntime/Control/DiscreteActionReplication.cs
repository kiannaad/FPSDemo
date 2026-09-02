using System;

namespace CGame
{
    public enum DiscreteActionKind
    {
        Reload,
        Melee,
        Equip,
        Unequip,
        Recoil
    }

    public interface IDiscreteActionReplicationGateway
    {
        long BeginPredicted(DiscreteActionKind actionKind, string variantId, long equipmentInstanceId);
        long BeginPredicted(
            DiscreteActionKind actionKind,
            string variantId,
            long equipmentInstanceId,
            int durationTicks,
            int? commitOffsetTicks);
        void RegisterPredictedPlayback(long predictionNonce, object playbackHandle);
        void RegisterCommit(long predictionNonce, Action callback);
    }
}
