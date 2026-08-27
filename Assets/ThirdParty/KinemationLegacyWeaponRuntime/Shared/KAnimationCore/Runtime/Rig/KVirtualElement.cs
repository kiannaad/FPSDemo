// Copyright (c) 2026 CGame.KinemationLegacyWeaponRuntime.KINEMATION.
// All rights reserved.

using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Rig
{
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Rig")]
    public class KVirtualElement : MonoBehaviour
    {
        public Transform targetBone;

        public void Animate()
        {
            transform.position = targetBone.position;
            transform.rotation = targetBone.rotation;
        }
    }
}