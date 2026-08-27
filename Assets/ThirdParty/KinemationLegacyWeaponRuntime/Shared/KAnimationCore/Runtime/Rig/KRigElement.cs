// Copyright (c) 2026 CGame.KinemationLegacyWeaponRuntime.KINEMATION.
// All rights reserved.

using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CGame.KinemationLegacyWeaponRuntime.KINEMATION.Shared.KAnimationCore.Runtime.Rig
{
    [MovedFrom("CGame.KinemationLegacyWeaponRuntime.KINEMATION.KAnimationCore.Runtime.Rig")]
    [Serializable]
    public struct KRigElement
    {
        public string name;
        [HideInInspector] public int index;
        public int depth;

        public KRigElement(int index = -1, string name = "None", int depth = -1)
        {
            this.index = index;
            this.name = name;
            this.depth = depth;
        }
    }
}