using UnityEngine;

namespace CGame
{
    public abstract class ItemFragment : ScriptableObject
    {
        public abstract void Initialize(ItemInstance itemInstance);
    }
}
