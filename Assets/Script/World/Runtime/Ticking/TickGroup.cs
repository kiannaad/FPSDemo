using System;

namespace CGame
{
    public enum TickGroup
    {
        TG_PrePhysics = 0,
        TG_PhysicsMovement = 1,
        TG_Input = 100,
        TG_PostPhysics = 101,
        TG_GameMode = 102,
        TG_Controller = 103,
        TG_Gameplay = 104,
        TG_PreAnimation = 200,
        TG_PostAnimation = 201,
        TG_CharacterPresentation = 202,
        TG_Camera = TG_CharacterPresentation,
        TG_LatePresentation = 203
    }

    internal static class TickGroupUtility
    {
        public static TickDomain GetDomain(TickGroup group)
        {
            if (group == TickGroup.TG_PreAnimation)
            {
                return TickDomain.PreAnimation;
            }

            int value = (int)group;
            if (value < 100)
            {
                return TickDomain.Fixed;
            }

            if (value < 200)
            {
                return TickDomain.Update;
            }

            if (value < 300)
            {
                return TickDomain.Late;
            }

            throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown TickGroup.");
        }
    }
}
