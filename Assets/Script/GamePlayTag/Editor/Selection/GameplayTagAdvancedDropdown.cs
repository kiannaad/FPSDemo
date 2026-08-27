using System;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public sealed class GameplayTagAdvancedDropdown : AdvancedDropdown
    {
        private readonly GameplayTagPickerModel model;
        private readonly Action<string> onSelected;

        public GameplayTagAdvancedDropdown(
            AdvancedDropdownState state,
            GameplayTagPickerModel model,
            Action<string> onSelected) : base(state)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
            minimumSize = new Vector2(360f, 280f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Gameplay Tags");
            root.AddChild(new TagItem("<None>", string.Empty));
            foreach (string name in model.Names)
            {
                root.AddChild(new TagItem(name, name));
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TagItem tagItem)
            {
                onSelected(tagItem.Value);
            }
        }

        private sealed class TagItem : AdvancedDropdownItem
        {
            public TagItem(string displayName, string value) : base(displayName)
            {
                Value = value;
            }

            public string Value { get; }
        }
    }
}
