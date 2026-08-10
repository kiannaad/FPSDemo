using UnityEngine;

namespace CGame
{
    public abstract class ControllerDefinition : ScriptableObject
    {
        public abstract PlayerController CreateController(PlayerControllerCreationContext context);
    }
}
