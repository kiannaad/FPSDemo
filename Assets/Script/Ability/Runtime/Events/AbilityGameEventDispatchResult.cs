using System;
using System.Collections.Generic;

namespace CGame.Ability
{
    public sealed class AbilityGameEventDispatchResult
    {
        private readonly Exception[] exceptions;

        internal AbilityGameEventDispatchResult(int matchedCount, int invokedCount, IReadOnlyList<Exception> exceptions)
        {
            MatchedCount = matchedCount;
            InvokedCount = invokedCount;
            this.exceptions = exceptions == null ? Array.Empty<Exception>() : new List<Exception>(exceptions).ToArray();
        }

        public int MatchedCount { get; }
        public int InvokedCount { get; }
        public IReadOnlyList<Exception> Exceptions => exceptions;

        internal static AbilityGameEventDispatchResult Empty { get; } =
            new AbilityGameEventDispatchResult(0, 0, Array.Empty<Exception>());
    }
}
