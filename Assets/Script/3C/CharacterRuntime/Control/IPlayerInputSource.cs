using UnityEngine;

namespace CGame
{
    public interface IPlayerInputSource
    {
        CharacterControlIntent ReadControlIntent();

        Vector2 ReadLookDelta(float deltaTime);

        bool FirePressed { get; }

        bool ReloadPressed { get; }

        bool MeleePressed { get; }

        int RequestedQuickBarSlot { get; }
    }
}
