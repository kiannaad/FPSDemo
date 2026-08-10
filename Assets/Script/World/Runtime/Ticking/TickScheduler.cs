using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class TickScheduler
    {
        private sealed class Node
        {
            public long Id;
            public string Name;
            public TickGroup Group;
            public Action<float> Callback;
            public bool Critical;
            public bool Active = true;
            public bool Enabled = true;
            public bool Faulted;
            public bool Committed;
            public readonly HashSet<long> Prerequisites = new HashSet<long>();
        }

        private readonly Dictionary<long, Node> nodes = new Dictionary<long, Node>();
        private readonly List<long> pendingAdditions = new List<long>();
        private readonly List<long> pendingRemovals = new List<long>();
        private readonly List<TickFault> faults = new List<TickFault>();
        private long nextRegistrationId;
        private bool isExecuting;

        public event Action<TickFault> Faulted;

        public IReadOnlyList<TickFault> Faults => faults;

        public TickFunctionHandle Register(
            string name,
            TickGroup group,
            Action<float> callback,
            bool critical = false)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            long id = ++nextRegistrationId;
            Node node = new Node
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? $"TickFunction#{id}" : name,
                Group = group,
                Callback = callback,
                Critical = critical
            };
            nodes.Add(id, node);
            pendingAdditions.Add(id);
            return new TickFunctionHandle(this, id, node.Name, group);
        }

        public void ExecuteDomain(TickDomain domain, float deltaTime)
        {
            if (isExecuting)
            {
                throw new InvalidOperationException("TickScheduler does not allow recursive domain execution.");
            }

            CommitMutations();
            isExecuting = true;
            try
            {
                foreach (TickGroup group in GetGroups(domain))
                {
                    ExecuteGroup(group, deltaTime);
                }
            }
            finally
            {
                isExecuting = false;
                CommitMutations();
            }
        }

        internal bool IsRegistered(long id) => nodes.TryGetValue(id, out Node node) && node.Active;

        internal bool IsEnabled(long id) =>
            nodes.TryGetValue(id, out Node node) && node.Active && node.Enabled;

        internal bool IsFaulted(long id) =>
            nodes.TryGetValue(id, out Node node) && node.Active && node.Faulted;

        internal void SetEnabled(long id, bool enabled)
        {
            Node node = GetActiveNode(id);
            if (node.Faulted && enabled)
            {
                throw new InvalidOperationException($"Faulted TickFunction {node.Name} cannot be re-enabled.");
            }

            node.Enabled = enabled;
        }

        internal void AddPrerequisite(TickFunctionHandle dependent, TickFunctionHandle prerequisite)
        {
            if (dependent == null)
            {
                throw new ArgumentNullException(nameof(dependent));
            }

            if (prerequisite == null)
            {
                throw new ArgumentNullException(nameof(prerequisite));
            }

            if (!dependent.BelongsTo(this) || !prerequisite.BelongsTo(this))
            {
                throw new InvalidOperationException("Tick prerequisites must belong to the same scheduler.");
            }

            Node dependentNode = GetActiveNode(dependent.RegistrationId);
            Node prerequisiteNode = GetActiveNode(prerequisite.RegistrationId);
            if (dependentNode.Group != prerequisiteNode.Group)
            {
                TickDomain dependentDomain = TickGroupUtility.GetDomain(dependentNode.Group);
                TickDomain prerequisiteDomain = TickGroupUtility.GetDomain(prerequisiteNode.Group);
                string reason = dependentDomain != prerequisiteDomain
                    ? "Cross-domain prerequisites are not allowed."
                    : "Prerequisites must be within the same TickGroup.";
                throw new InvalidOperationException(reason);
            }

            if (dependentNode.Id == prerequisiteNode.Id || DependsOn(prerequisiteNode.Id, dependentNode.Id))
            {
                throw new InvalidOperationException("Tick prerequisite would create a cycle.");
            }

            dependentNode.Prerequisites.Add(prerequisiteNode.Id);
        }

        internal void Unregister(long id)
        {
            if (!nodes.TryGetValue(id, out Node node) || !node.Active)
            {
                return;
            }

            node.Active = false;
            node.Enabled = false;
            pendingRemovals.Add(id);
            if (!isExecuting)
            {
                CommitMutations();
            }
        }

        private void ExecuteGroup(TickGroup group, float deltaTime)
        {
            List<Node> ordered = BuildExecutionOrder(group);
            HashSet<long> skipped = new HashSet<long>();
            for (int index = 0; index < ordered.Count; index++)
            {
                Node node = ordered[index];
                if (!node.Active || !node.Enabled || node.Faulted || HasUnavailablePrerequisite(node, skipped))
                {
                    skipped.Add(node.Id);
                    continue;
                }

                try
                {
                    node.Callback(deltaTime);
                }
                catch (Exception exception)
                {
                    node.Faulted = true;
                    node.Enabled = false;
                    skipped.Add(node.Id);
                    var fault = new TickFault(
                        new TickFunctionHandle(this, node.Id, node.Name, node.Group),
                        exception,
                        node.Critical);
                    faults.Add(fault);
                    Faulted?.Invoke(fault);
                }
            }
        }

        private List<Node> BuildExecutionOrder(TickGroup group)
        {
            List<Node> groupNodes = new List<Node>();
            foreach (Node node in nodes.Values)
            {
                if (node.Active && node.Committed && node.Group == group)
                {
                    groupNodes.Add(node);
                }
            }

            groupNodes.Sort((left, right) => left.Id.CompareTo(right.Id));
            List<Node> ordered = new List<Node>(groupNodes.Count);
            HashSet<long> emitted = new HashSet<long>();
            while (ordered.Count < groupNodes.Count)
            {
                Node next = null;
                for (int index = 0; index < groupNodes.Count; index++)
                {
                    Node candidate = groupNodes[index];
                    if (emitted.Contains(candidate.Id) || !AllGroupPrerequisitesEmitted(candidate, emitted))
                    {
                        continue;
                    }

                    next = candidate;
                    break;
                }

                if (next == null)
                {
                    throw new InvalidOperationException($"Tick prerequisite cycle detected in {group}.");
                }

                emitted.Add(next.Id);
                ordered.Add(next);
            }

            return ordered;
        }

        private bool AllGroupPrerequisitesEmitted(Node node, HashSet<long> emitted)
        {
            foreach (long prerequisiteId in node.Prerequisites)
            {
                if (nodes.TryGetValue(prerequisiteId, out Node prerequisite)
                    && prerequisite.Active
                    && prerequisite.Committed
                    && prerequisite.Group == node.Group
                    && !emitted.Contains(prerequisiteId))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasUnavailablePrerequisite(Node node, HashSet<long> skipped)
        {
            foreach (long prerequisiteId in node.Prerequisites)
            {
                if (skipped.Contains(prerequisiteId)
                    || !nodes.TryGetValue(prerequisiteId, out Node prerequisite)
                    || !prerequisite.Active
                    || !prerequisite.Enabled
                    || prerequisite.Faulted)
                {
                    return true;
                }
            }

            return false;
        }

        private bool DependsOn(long startId, long targetId)
        {
            Stack<long> pending = new Stack<long>();
            HashSet<long> visited = new HashSet<long>();
            pending.Push(startId);
            while (pending.Count > 0)
            {
                long currentId = pending.Pop();
                if (!visited.Add(currentId) || !nodes.TryGetValue(currentId, out Node current))
                {
                    continue;
                }

                foreach (long prerequisiteId in current.Prerequisites)
                {
                    if (prerequisiteId == targetId)
                    {
                        return true;
                    }

                    pending.Push(prerequisiteId);
                }
            }

            return false;
        }

        private Node GetActiveNode(long id)
        {
            if (!nodes.TryGetValue(id, out Node node) || !node.Active)
            {
                throw new InvalidOperationException($"TickFunction {id} is not registered.");
            }

            return node;
        }

        private void CommitMutations()
        {
            for (int index = 0; index < pendingRemovals.Count; index++)
            {
                long removedId = pendingRemovals[index];
                nodes.Remove(removedId);
                foreach (Node node in nodes.Values)
                {
                    node.Prerequisites.Remove(removedId);
                }
            }

            pendingRemovals.Clear();
            for (int index = 0; index < pendingAdditions.Count; index++)
            {
                if (nodes.TryGetValue(pendingAdditions[index], out Node node) && node.Active)
                {
                    node.Committed = true;
                }
            }

            pendingAdditions.Clear();
        }

        private static IEnumerable<TickGroup> GetGroups(TickDomain domain)
        {
            switch (domain)
            {
                case TickDomain.Fixed:
                    yield return TickGroup.TG_PrePhysics;
                    yield return TickGroup.TG_CharacterMotorSimulation;
                    break;
                case TickDomain.Update:
                    yield return TickGroup.TG_Input;
                    yield return TickGroup.TG_PostPhysics;
                    yield return TickGroup.TG_GameMode;
                    yield return TickGroup.TG_Controller;
                    yield return TickGroup.TG_Gameplay;
                    yield return TickGroup.TG_PreAnimation;
                    break;
                case TickDomain.Late:
                    yield return TickGroup.TG_PostAnimation;
                    yield return TickGroup.TG_CharacterPresentation;
                    yield return TickGroup.TG_LatePresentation;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(domain), domain, null);
            }
        }
    }
}
