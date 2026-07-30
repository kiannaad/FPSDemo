using System.Collections.Generic;

namespace CGame.Animation
{
    public sealed class WeaponAnimationDefinitionLocationResolver :
        IWeaponAnimationDefinitionLocationResolver
    {
        private static readonly IReadOnlyDictionary<WeaponId, string>
            Locations = new Dictionary<WeaponId, string>
            {
                {
                    new WeaponId("knife"),
                    "KnifeWeaponAnimationDefinition"
                },
                {
                    new WeaponId("rifle"),
                    "RifleAKAnimationDefinition"
                },
            };

        public bool TryResolveLocation(
            WeaponId weaponId,
            out string location)
        {
            if (!weaponId.IsValid)
            {
                location = string.Empty;
                return false;
            }

            return Locations.TryGetValue(weaponId, out location);
        }
    }
}
