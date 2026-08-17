using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor
{
    public static class AnimationLayerTypeDropdown
    {
        private static readonly Type[] supportedTypes =
        {
            typeof(PoseSamplerLayerSettings),
            typeof(PoseOffsetLayerSettings),
            typeof(AttachHandLayerSettings),
            typeof(ViewLayerSettings),
            typeof(LookLayerSettings),
            typeof(TurnLayerSettings),
            typeof(IkLayerSettings)
        };

        public static IReadOnlyList<Type> SupportedTypes => supportedTypes;

        public static void Show(Rect buttonRect, Action<Type> onSelected)
        {
            if (onSelected == null)
            {
                throw new ArgumentNullException(nameof(onSelected));
            }

            GenericMenu menu = new GenericMenu();
            Add(menu, "Pose/Pose Sampler", typeof(PoseSamplerLayerSettings), onSelected);
            Add(menu, "Pose/Pose Offset", typeof(PoseOffsetLayerSettings), onSelected);
            Add(menu, "Hands/Attach Hand", typeof(AttachHandLayerSettings), onSelected);
            Add(menu, "View/View", typeof(ViewLayerSettings), onSelected);
            Add(menu, "View/Look", typeof(LookLayerSettings), onSelected);
            Add(menu, "View/Turn", typeof(TurnLayerSettings), onSelected);
            Add(menu, "IK/IK", typeof(IkLayerSettings), onSelected);
            menu.DropDown(buttonRect);
        }

        public static bool IsSupported(Type type)
        {
            for (int index = 0; index < supportedTypes.Length; index++)
            {
                if (supportedTypes[index] == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Add(GenericMenu menu, string label, Type type, Action<Type> onSelected)
        {
            menu.AddItem(new GUIContent(label), false, () => onSelected(type));
        }
    }
}
