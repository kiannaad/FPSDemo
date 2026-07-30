// Copyright (c) 2026 CGame.KinemationLegacyWeaponRuntime.KINEMATION.
// All rights reserved.

using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Input
{
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Input")]
    [Serializable]
    public struct BoolProperty
    {
        public string name;
        public bool defaultValue;
    }
    
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Input")]
    [Serializable]
    public struct IntProperty
    {
        public string name;
        public int defaultValue;
    }
    
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Input")]
    [Serializable]
    public struct FloatProperty
    {
        public string name;
        public float defaultValue;
        public float interpolationSpeed;
    }
    
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Input")]
    [Serializable]
    public struct VectorProperty
    {
        public string name;
        public Vector4 defaultValue;
    }
}