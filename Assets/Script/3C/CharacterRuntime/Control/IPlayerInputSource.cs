using UnityEngine;

namespace CGame
{
    public interface IPlayerInputSource
    {
        InputHandle InputHandle { get; }

        CharacterControlIntent ReadControlIntent();

        Vector2 ReadLookDelta(float deltaTime);

        bool FirePressed { get; }

        bool ReloadPressed { get; }

        bool MeleePressed { get; }

        int RequestedQuickBarSlot { get; }
    }
}
