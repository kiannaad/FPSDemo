using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    internal static class GameplayTagDrawerGui
    {
        public static void DrawTagButton(
            Rect position,
            GUIContent label,
            string currentName,
            Action<string> onSelected)
        {
            Rect valueRect = EditorGUI.PrefixLabel(position, label);
            bool hasModel = GameplayTagPickerModel.TryCreateFromAuthoritativeConfig(out GameplayTagPickerModel model, out string error);
            bool invalid = !string.IsNullOrEmpty(currentName) && (!hasModel || !model.IsExplicit(currentName));
            string displayName = string.IsNullOrEmpty(currentName) ? "<None>" : currentName;

            Color previousColor = GUI.color;
            if (invalid)
            {
                GUI.color = new Color(1f, 0.45f, 0.45f);
            }

            if (EditorGUI.DropdownButton(valueRect, new GUIContent(displayName, invalid ? "Unregistered GameplayTag" : string.Empty), FocusType.Keyboard))
            {
                if (hasModel)
                {
                    var dropdown = new GameplayTagAdvancedDropdown(new AdvancedDropdownState(), model, onSelected);
                    dropdown.Show(valueRect);
                }
                else
                {
                    Debug.LogError(error);
                }
            }

            GUI.color = previousColor;
        }
    }
}
