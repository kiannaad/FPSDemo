// Copyright (c) 2026 CGame.KinemationLegacyWeaponRuntime.KINEMATION.
// All rights reserved.

using System;
using CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Core;
using UnityEngine.Scripting.APIUpdating;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Rig
{
    // Represents the space we will modify bone transform in.
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Rig")]
    public enum ESpaceType
    {
        BoneSpace,
        ParentBoneSpace,
        ComponentSpace,
        WorldSpace
    }

    // Whether the operation is additive or absolute.
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Rig")]
    public enum EModifyMode
    {
        Add,
        Replace,
        Ignore
    }
    
    // Represents the pose for the specific rig element.
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Rig")]
    [Serializable]
    public struct KPose
    {
        public KRigElement element;
        public KTransform pose;
        public ESpaceType space;
        public EModifyMode modifyMode;
    }
}