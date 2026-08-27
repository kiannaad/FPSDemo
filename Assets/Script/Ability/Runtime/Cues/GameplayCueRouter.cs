namespace CGame.Ability.Cues
{
    public static class GameplayCueRouter
    {
        private static IGameplayCueRouter current;

        public static IGameplayCueRouter Current => current;

        public static void Register(IGameplayCueRouter router)
        {
            current = router;
        }

        public static void Unregister(IGameplayCueRouter router)
        {
            if (ReferenceEquals(current, router))
            {
                current = null;
            }
        }
    }
}
