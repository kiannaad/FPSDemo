// Designed by KINEMATION, 2024.

using CGame.KinemationLegacyWeaponRuntime.KINEMATION.FPSAnimationFramework.Runtime.Core;
using CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Rig;

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.FPSAnimationFramework.Runtime.Layers.BlendingLayer
{
    [Serializable]
    public struct BlendingLayerElement
    {
        public KRigElement elementToBlend;
        [Range(0f, 1f)] public float weight;
        public bool cacheBlendedResult;
    }
    
    public class BlendingLayerSettings : FPSAnimatorLayerSettings
    {
        public AnimationClip desiredPose;
        public List<BlendingLayerElement> blendingElements = new List<BlendingLayerElement>();
        public bool blendPosition;

        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new BlendingLayerJob();
        }

#if UNITY_EDITOR
        public override void OnRigUpdated()
        {
            for (int i = 0; i < blendingElements.Count; i++)
            {
                var element = blendingElements[i];
                UpdateRigElement(ref element.elementToBlend);
                blendingElements[i] = element;
            }
        }
#endif
    }
}