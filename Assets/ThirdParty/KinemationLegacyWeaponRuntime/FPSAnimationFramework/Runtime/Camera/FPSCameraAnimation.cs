// Designed by KINEMATION, 2024.

using CGame.KinemationLegacyWeaponRuntime.KINEMATION.FPSAnimationFramework.Runtime.Core;
using CGame.KinemationLegacyWeaponRuntime.KINEMATION.FPSAnimationFramework.Runtime.Playables;
using CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Core;
using UnityEngine;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.FPSAnimationFramework.Runtime.Camera
{
    [CreateAssetMenu(fileName = "NewCameraAnimation", menuName = FPSANames.FileMenuGeneral + "Camera Animation")]
    public class FPSCameraAnimation : ScriptableObject
    {
        public VectorCurve translation = VectorCurve.Constant(0f, 1f, 0f);
        public VectorCurve rotation = VectorCurve.Constant(0f, 1f, 0f);
        public BlendTime blendTime = new BlendTime(0.15f, 0.15f);
        [Range(0f, 1f)] public float scale = 1f;
    }
}
