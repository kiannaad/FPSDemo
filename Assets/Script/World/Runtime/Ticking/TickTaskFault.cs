using System;

namespace CGame
{
    public sealed class TickTaskFault
    {
        public TickTaskFault(object owner, TickTaskNode node, Exception exception, bool critical)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Critical = critical;
        }

        public object Owner { get; }

        public TickTaskNode Node { get; }

        public Exception Exception { get; }

        public bool Critical { get; }
    }
}
