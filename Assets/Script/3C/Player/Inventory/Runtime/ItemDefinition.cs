using System;
using UnityEngine;

namespace CGame
{
    public abstract class ItemDefinition : ScriptableObject
    {
        [SerializeField] private ItemFragment[] fragments = Array.Empty<ItemFragment>();

        public virtual ItemInstance CreateInstance(ItemInstanceHandle handle)
        {
            var instance = new ItemInstance(handle, this);
            for (int index = 0; index < fragments.Length; index++)
            {
                if (fragments[index] == null)
                {
                    throw new InvalidOperationException("ItemDefinition contains a null fragment.");
                }

                fragments[index].Initialize(instance);
            }

            return instance;
        }
    }
}
