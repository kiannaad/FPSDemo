using CGame;
using System;
using CGame.Ability;
using CGame.Animation;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "CGame/Weapon/Definition")]
    public sealed class WeaponDefinition : EquipmentDefinition
    {
        [SerializeField] private GameplayTag weaponTag;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string animatorPath;
        [SerializeField] private AnimationClipAsset overlayPose;
        [SerializeField] private BoneProfile armedProfile;
        [SerializeField] private HandPalmCalibration handPalmCalibration;
        [SerializeField] private WeaponIkMotion equipIkMotion;
        [SerializeField] private WeaponIkMotion unequipIkMotion;
        [SerializeField] private Vector3 presentationLocalPosition;
        [SerializeField] private Vector3 presentationLocalEulerAngles;
        [SerializeField] private Vector3 presentationLocalScale = Vector3.one;
        [SerializeField] private WeaponAbilitySetDefinition abilitySet = new WeaponAbilitySetDefinition();
        [SerializeField] private int magazineCapacity = 30;
        [SerializeField] private RecoilProfile recoilProfile;
        [SerializeField, Range(1f, 179f)] private float aimFov = 40f;
        [SerializeField, Min(0.001f)] private float fireInterval = 0.1f;
        [SerializeField] private int loadTicks = 1;
        [SerializeField] private bool simulateLoadFailure;
        [NonSerialized] private AbilitySet runtimeAbilitySet;

        public GameplayTag WeaponTag => weaponTag;
        public GameObject Prefab => prefab;
        public string AnimatorPath => animatorPath ?? string.Empty;
        public AnimationClipAsset OverlayPose => overlayPose;
        public BoneProfile ArmedProfile => armedProfile;
        public HandPalmCalibration HandPalmCalibration => handPalmCalibration;
        public WeaponIkMotion EquipIkMotion => equipIkMotion;
        public WeaponIkMotion UnequipIkMotion => unequipIkMotion;
        public Vector3 PresentationLocalPosition => presentationLocalPosition;
        public Quaternion PresentationLocalRotation => Quaternion.Euler(presentationLocalEulerAngles);
        public Vector3 PresentationLocalScale => presentationLocalScale;
        public WeaponAbilitySetDefinition AbilitySet => abilitySet;
        public int MagazineCapacity => magazineCapacity;
        public RecoilProfile RecoilProfile => recoilProfile;
        public float AimFov => aimFov;
        public float FireInterval => fireInterval;
        public override int LoadTicks => Math.Max(0, loadTicks);
        public override bool SimulateLoadFailure => simulateLoadFailure;

        public override EquipmentInstance CreateInstance(EquipmentCreateContext context)
        {
            Validate();
            return new WeaponInstance(context, this);
        }

        public AbilitySet CreateAbilitySet()
        {
            return runtimeAbilitySet ?? abilitySet.CreateAbilitySet();
        }

        public void ConfigurePresentation(
            GameplayTag tag,
            GameObject weaponPrefab,
            string weaponAnimatorPath,
            AnimationClipAsset pose,
            BoneProfile profile,
            WeaponIkMotion equipMotion = null,
            WeaponIkMotion unequipMotion = null)
        {
            weaponTag = tag;
            prefab = weaponPrefab;
            animatorPath = weaponAnimatorPath;
            overlayPose = pose;
            armedProfile = profile;
            equipIkMotion = equipMotion;
            unequipIkMotion = unequipMotion;
        }

        public void ConfigurePresentationTransform(Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
        {
            if (localScale.x <= 0f || localScale.y <= 0f || localScale.z <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(localScale), "Weapon presentation scale must be positive.");
            }

            presentationLocalPosition = localPosition;
            presentationLocalEulerAngles = localEulerAngles;
            presentationLocalScale = localScale;
        }

        public void ConfigureHandPalmCalibration(HandPalmCalibration calibration)
        {
            handPalmCalibration = calibration != null
                ? calibration
                : throw new ArgumentNullException(nameof(calibration));
        }

        public void ConfigureAimFov(float value)
        {
            if (value <= 0f || value >= 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Aim FOV must be between 0 and 180 degrees.");
            }

            aimFov = value;
        }

        public static WeaponDefinition CreateRuntime(
            GameplayTag weaponTag,
            int magazineCapacity,
            AbilitySet abilitySet)
        {
            if (weaponTag.IsEmpty)
            {
                throw new ArgumentException("Weapon tag cannot be empty.", nameof(weaponTag));
            }

            WeaponDefinition definition = CreateInstance<WeaponDefinition>();
            definition.weaponTag = weaponTag;
            definition.magazineCapacity = Math.Max(1, magazineCapacity);
            definition.runtimeAbilitySet = abilitySet ?? throw new ArgumentNullException(nameof(abilitySet));
            return definition;
        }

        public void Validate()
        {
            if (weaponTag.IsEmpty)
            {
                throw new InvalidOperationException("Weapon Definition requires an exact leaf GameplayTag.");
            }

            if (magazineCapacity <= 0)
            {
                throw new InvalidOperationException("Weapon Definition magazine capacity must be positive.");
            }

            if (fireInterval <= 0f)
            {
                throw new InvalidOperationException("Weapon Definition fire interval must be positive.");
            }

            if (aimFov <= 0f || aimFov >= 180f)
            {
                throw new InvalidOperationException("Weapon Definition aim FOV must be between 0 and 180 degrees.");
            }

            if (abilitySet == null && runtimeAbilitySet == null)
            {
                throw new InvalidOperationException("Weapon Definition requires an AbilitySet definition.");
            }

            if (prefab != null && (overlayPose == null || armedProfile == null || equipIkMotion == null || unequipIkMotion == null))
            {
                throw new InvalidOperationException("Serialized Weapon Definition requires overlay, profile, equip and unequip IK Motion.");
            }

            if (prefab != null && (presentationLocalScale.x <= 0f || presentationLocalScale.y <= 0f || presentationLocalScale.z <= 0f))
            {
                throw new InvalidOperationException("Serialized Weapon Definition presentation scale must be positive.");
            }
        }
    }
}
