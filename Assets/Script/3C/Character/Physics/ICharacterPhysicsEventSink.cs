using UnityEngine;

namespace CGame
{
    internal interface ICharacterPhysicsEventSink
    {
        void Enqueue(ICharacterPhysicsController controller, Collider collider);
    }
}
