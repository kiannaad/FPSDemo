// Copyright (c) 2026 CGame.KinemationLegacyWeaponRuntime.KINEMATION.
// All rights reserved.

using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Attributes
{
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Attributes")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class CurveSelectorAttribute : PropertyAttribute
    {
        public bool useAnimator;
        public bool usePlayables;
        public bool useInput;
        
        public CurveSelectorAttribute(bool useAnimator = true, bool usePlayables = true, bool useInput = true)
        {
            this.useAnimator = useAnimator;
            this.usePlayables = usePlayables;
            this.useInput = useInput;
        }
    }

    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Attributes")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class InputProperty : PropertyAttribute { }
    
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Attributes")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class RigAssetSelectorAttribute : PropertyAttribute
    {
        public string assetName;
        
        public RigAssetSelectorAttribute(string rigName = "")
        {
            assetName = rigName;
        }
    }

    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Attributes")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class ElementChainSelectorAttribute : RigAssetSelectorAttribute
    {
        public ElementChainSelectorAttribute(string rigName = "")
        {
            assetName = rigName;
        }
    }
    
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Attributes")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class ReadOnlyAttribute : PropertyAttribute { }

    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Attributes")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class UnfoldAttribute : PropertyAttribute { }

    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Attributes")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class TabAttribute : PropertyAttribute
    {
        public string tabName;

        public TabAttribute(string tabName)
        {
            this.tabName = tabName;
        }
    }
    
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Attributes")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class CustomElementChainDrawerAttribute : PropertyAttribute
    {
        public bool drawLabel;
        public bool drawTextField;

        public CustomElementChainDrawerAttribute(bool drawLabel, bool drawTextField)
        {
            this.drawLabel = drawLabel;
            this.drawTextField = drawTextField;
        }
    }

    public class KAttributes { }
}