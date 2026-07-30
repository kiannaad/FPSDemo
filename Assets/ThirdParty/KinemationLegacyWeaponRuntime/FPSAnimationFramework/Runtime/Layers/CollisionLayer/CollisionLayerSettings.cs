// Designed by KINEMATION, 2024.

using CGame.KinemationLegacyWeaponRuntime.KINEMATION.FPSAnimationFramework.Runtime.Core;
using CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Core;
using CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using UnityEngine;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.FPSAnimationFramework.Runtime.Layers.CollisionLayer
{
    public class CollisionLayerSettings : FPSAnimatorLayerSettings
    {
        public KRigElement weaponIkBone = new KRigElement(-1, FPSANames.IkWeaponBone);
        public KTransform primaryPose = KTransform.Identity;
        public KTransform secondaryPose = KTransform.Identity;
        public ESpaceType targetSpace = ESpaceType.ComponentSpace;
        public bool useSecondaryPose;
        [Min(0f)] public float smoothingSpeed = 0f;
        
        [Header("Raycast")]
        public LayerMask layerMask;
        [Min(0f)] public float rayStartOffset = 0f;
        [Min(0f)] public float barrelLength = 0f;
        
        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new CollisionLayerJob();
        }

#if UNITY_EDITOR
        public override void OnRigUpdated()
        {
            UpdateRigElement(ref weaponIkBone);
        }
#endif
    }
}