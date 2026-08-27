// Copyright (c) 2026 CGame.KinemationLegacyWeaponRuntime.KINEMATION.
// All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Input
{
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Input")]
    [CreateAssetMenu(fileName = "NewInputConfig", menuName = "KINEMATION/Input Config")]
    public class UserInputConfig : ScriptableObject
    {
        public List<IntProperty> intProperties = new List<IntProperty>();
        public List<FloatProperty> floatProperties = new List<FloatProperty>();
        public List<BoolProperty> boolProperties = new List<BoolProperty>();
        public List<VectorProperty> vectorProperties = new List<VectorProperty>();
    }
}