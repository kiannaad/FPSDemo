using System;

namespace CGame.Animation
{
    public static class WeaponBoneProfileValidator
    {
        private static readonly Type[] KnifeLayerOrder =
        {
            typeof(PoseSamplerLayerSettings),
            typeof(IkLayerSettings)
        };

        private static readonly Type[] Ak12LegacyLayerOrder =
        {
            typeof(PoseSamplerLayerSettings),
            typeof(IkLayerSettings)
        };

        private static readonly Type[] Ak12ProceduralLayerOrder =
        {
            typeof(PoseSamplerLayerSettings),
            typeof(AttachHandLayerSettings),
            typeof(ViewLayerSettings),
            typeof(LookLayerSettings),
            typeof(TurnLayerSettings),
            typeof(IkLayerSettings)
        };

        public static void ValidateKnife(BoneProfile profile)
        {
            Validate(profile, "Knife", KnifeLayerOrder);
        }

        public static void ValidateAk12(BoneProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            Type[] expectedOrder = profile.Layers.Count == Ak12ProceduralLayerOrder.Length
                ? Ak12ProceduralLayerOrder
                : Ak12LegacyLayerOrder;
            Validate(profile, "AK12", expectedOrder);
        }

        private static void Validate(BoneProfile profile, string weaponName, Type[] expectedOrder)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.Layers.Count != expectedOrder.Length)
            {
                throw new InvalidOperationException(
                    $"{weaponName} Bone Profile requires exactly {expectedOrder.Length} layers.");
            }

            for (int index = 0; index < expectedOrder.Length; index++)
            {
                AnimationLayerSettings settings = profile.Layers[index];
                if (settings.GetType() != expectedOrder[index])
                {
                    throw new InvalidOperationException(
                        $"{weaponName} Bone Profile layer {index} must be {expectedOrder[index].Name}, but found {settings.GetType().Name}.");
                }
            }
        }
    }
}
