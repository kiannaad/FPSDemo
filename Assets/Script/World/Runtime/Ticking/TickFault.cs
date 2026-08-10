using System;

namespace CGame
{
    public sealed class TickFault
    {
        public TickFault(TickFunctionHandle handle, Exception exception, bool critical)
        {
            Handle = handle;
            Exception = exception;
            Critical = critical;
        }

        public TickFunctionHandle Handle { get; }

        public Exception Exception { get; }

        public bool Critical { get; }
    }
}
