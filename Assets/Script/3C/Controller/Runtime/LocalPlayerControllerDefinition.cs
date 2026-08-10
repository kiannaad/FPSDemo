using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "LocalPlayerControllerDefinition", menuName = "CGame/Gameplay/Local Player Controller")]
    public sealed class LocalPlayerControllerDefinition : ControllerDefinition
    {
        public override PlayerController CreateController(PlayerControllerCreationContext context)
        {
            return PlayerController.Create(context, new DefaultPlayerControllerComponentFactory());
        }
    }
}
