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
        void RegisterCommit(long predictionNonce, Action<DiscreteActionCommit> callback);
    }

    public readonly struct DiscreteActionCommit
    {
        public DiscreteActionCommit(int? authoritativeMagazineAmmo, int? authoritativeReserveAmmo)
        {
            AuthoritativeMagazineAmmo = authoritativeMagazineAmmo;
            AuthoritativeReserveAmmo = authoritativeReserveAmmo;
        }

        public int? AuthoritativeMagazineAmmo { get; }
        public int? AuthoritativeReserveAmmo { get; }
        public bool HasAuthoritativeAmmo => AuthoritativeMagazineAmmo.HasValue && AuthoritativeReserveAmmo.HasValue;
    }
}
