using System;

namespace CGame
{
    public readonly struct WeaponRuntimeCapabilities :
        IEquatable<WeaponRuntimeCapabilities>
    {
        public WeaponRuntimeCapabilities(
            bool supportsFire,
            bool supportsReload,
            bool supportsMeleeAttack)
        {
            SupportsFire = supportsFire;
            SupportsReload = supportsReload;
            SupportsMeleeAttack = supportsMeleeAttack;
        }

        public bool SupportsFire { get; }
        public bool SupportsReload { get; }
        public bool SupportsMeleeAttack { get; }
        public bool HasValidPrimaryAction =>
            SupportsFire != SupportsMeleeAttack;
        public bool IsValid => HasValidPrimaryAction;

        public bool Equals(WeaponRuntimeCapabilities other)
        {
            return SupportsFire == other.SupportsFire
                && SupportsReload == other.SupportsReload
                && SupportsMeleeAttack == other.SupportsMeleeAttack;
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponRuntimeCapabilities other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = SupportsFire.GetHashCode();
                hashCode = (hashCode * 397) ^ SupportsReload.GetHashCode();
                hashCode =
                    (hashCode * 397) ^ SupportsMeleeAttack.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(
            WeaponRuntimeCapabilities left,
            WeaponRuntimeCapabilities right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            WeaponRuntimeCapabilities left,
            WeaponRuntimeCapabilities right)
        {
            return !left.Equals(right);
        }
    }
}
