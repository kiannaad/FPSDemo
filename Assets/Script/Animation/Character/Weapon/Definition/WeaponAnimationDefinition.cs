using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(
        menuName = "CGame/Animation/Weapon Animation Definition",
        fileName = "WeaponAnimationDefinition")]
    public sealed class WeaponAnimationDefinition : ScriptableObject
    {
        [SerializeField] private string weaponId;
        [SerializeField] private GameObject weaponPrefab;
        [SerializeField] private AnimationClipAsset overlayPose;
        [SerializeField] private AnimationClipAsset equip;
        [SerializeField] private AnimationClipAsset unequip;
        [SerializeField] private bool supportsFire;
        [SerializeField] private AnimationClipAsset fire;
        [SerializeField] private bool supportsReload;
        [SerializeField] private AnimationClipAsset reload;
        [SerializeField] private bool supportsMeleeAttack;
        [SerializeField] private AnimationClipAsset meleeAttack;

        public WeaponId WeaponId => new WeaponId(weaponId);
        public GameObject WeaponPrefab => weaponPrefab;
        public AnimationClipAsset OverlayPose => overlayPose;
        public AnimationClipAsset Equip => equip;
        public AnimationClipAsset Unequip => unequip;
        public bool SupportsFire => supportsFire;
        public AnimationClipAsset Fire => fire;
        public bool SupportsReload => supportsReload;
        public AnimationClipAsset Reload => reload;
        public bool SupportsMeleeAttack => supportsMeleeAttack;
        public AnimationClipAsset MeleeAttack => meleeAttack;
        public WeaponRuntimeCapabilities Capabilities =>
            new WeaponRuntimeCapabilities(
                supportsFire,
                supportsReload,
                supportsMeleeAttack);
        public bool IsValid =>
            Validate() == WeaponAnimationDefinitionError.None;

        [System.Obsolete("Use WeaponPrefab.")]
        public GameObject PresentationPrefab => weaponPrefab;
        [System.Obsolete("Use OverlayPose.")]
        public AnimationClipAsset Idle => overlayPose;
        [System.Obsolete("Weapon locomotion remains Animator-native.")]
        public AnimationClipAsset Walk => overlayPose;
        [System.Obsolete("Weapon locomotion remains Animator-native.")]
        public AnimationClipAsset Run => overlayPose;
        [System.Obsolete("Weapon locomotion remains Animator-native.")]
        public AnimationClipAsset Stop => overlayPose;
        [System.Obsolete("Weapon model animation is outside the V1 definition.")]
        public AnimationClipAsset WeaponModelFire => fire;
        [System.Obsolete("Weapon model animation is outside the V1 definition.")]
        public AnimationClipAsset WeaponModelReload => reload;
        [System.Obsolete("Use clip-local blend times.")]
        public float BlendDuration =>
            overlayPose != null ? overlayPose.BlendInTime : 0f;
        [System.Obsolete("Aim is outside the V1 definition.")]
        public float AimYawRange => 0f;
        [System.Obsolete("Aim is outside the V1 definition.")]
        public float AimPitchUpRange => 0f;
        [System.Obsolete("Aim is outside the V1 definition.")]
        public float AimPitchDownRange => 0f;
        [System.Obsolete("Aim is outside the V1 definition.")]
        public float AimWeight => 0f;
        [System.Obsolete("Aim is outside the V1 definition.")]
        public float AimSmoothingTime => 0f;
        [System.Obsolete("IK is outside the V1 definition.")]
        public float LeftHandIkSmoothingTime => 0f;
        [System.Obsolete("Recoil is outside the V1 definition.")]
        public float RecoilImpulse => 0f;
        [System.Obsolete("Recoil is outside the V1 definition.")]
        public float RecoilMaxPitch => 0f;
        [System.Obsolete("Recoil is outside the V1 definition.")]
        public float RecoilDecayTime => 0.001f;

        [System.Obsolete("Weapon locomotion remains Animator-native.")]
        public bool HasPoseFor(string locomotionState)
        {
            return overlayPose != null && overlayPose.IsValid;
        }

        public WeaponAnimationDefinitionError Validate(
            WeaponId expectedId = default)
        {
            if (!WeaponId.IsValid)
            {
                return WeaponAnimationDefinitionError.InvalidWeaponId;
            }

            if (expectedId.IsValid && WeaponId != expectedId)
            {
                return WeaponAnimationDefinitionError.WeaponIdMismatch;
            }

            if (weaponPrefab == null)
            {
                return WeaponAnimationDefinitionError.MissingWeaponPrefab;
            }

            if (!IsValidAsset(overlayPose))
            {
                return WeaponAnimationDefinitionError.MissingOverlayPose;
            }

            if (!IsValidAsset(equip))
            {
                return WeaponAnimationDefinitionError.MissingEquip;
            }

            if (!IsValidAsset(unequip))
            {
                return WeaponAnimationDefinitionError.MissingUnequip;
            }

            if (supportsFire == supportsMeleeAttack)
            {
                return WeaponAnimationDefinitionError.InvalidPrimaryAction;
            }

            if (!MatchesCapability(supportsFire, fire))
            {
                return WeaponAnimationDefinitionError.InvalidFire;
            }

            if (!MatchesCapability(supportsReload, reload))
            {
                return WeaponAnimationDefinitionError.InvalidReload;
            }

            if (!MatchesCapability(supportsMeleeAttack, meleeAttack))
            {
                return WeaponAnimationDefinitionError.InvalidMeleeAttack;
            }

            return WeaponAnimationDefinitionError.None;
        }

        private static bool MatchesCapability(
            bool capability,
            AnimationClipAsset asset)
        {
            return capability ? IsValidAsset(asset) : asset == null;
        }

        private static bool IsValidAsset(AnimationClipAsset asset)
        {
            return asset != null && asset.IsValid;
        }
    }
}
