using System;

namespace CGame.Animation
{
    public static class WeaponBoneProfileValidator
    {
        private static readonly Type[] KnifeLayerOrder =
        {
            typeof(PoseSamplerLayerSettings),
            typeof(PoseOffsetLayerSettings)
        };

        private static readonly Type[] Ak12LayerOrder =
        {
            typeof(PoseSamplerLayerSettings),
            typeof(AttachHandLayerSettings),
            typeof(ViewLayerSettings),
            typeof(IkLayerSettings)
        };

        public static void ValidateKnife(BoneProfile profile)
        {
            Validate(profile, "Knife", KnifeLayerOrder);
        }

        public static void ValidateAk12(BoneProfile profile)
        {
            Validate(profile, "AK12", Ak12LayerOrder);
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
