using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class TickTaskManager
    {
        private sealed class OwnerRecord
        {
            public object Owner;
            public bool Critical;
            public bool Active = true;
            public bool Enabled;
            public bool Faulted;
            public readonly List<NodeRecord> Nodes = new List<NodeRecord>();
        }

        private sealed class NodeRecord
        {
            public TickTaskNode Node;
            public OwnerRecord Owner;
            public bool Active = true;
            public bool Committed;
        }

        private sealed class OwnerRegistration : IDisposable
        {
            private TickTaskManager manager;
            private object owner;

            public OwnerRegistration(TickTaskManager manager, object owner)
            {
                this.manager = manager;
                this.owner = owner;
            }

            public void Dispose()
            {
                TickTaskManager currentManager = manager;
                object currentOwner = owner;
                manager = null;
                owner = null;
                currentManager?.UnregisterOwner(currentOwner);
            }
        }

        private readonly Dictionary<object, OwnerRecord> owners = new Dictionary<object, OwnerRecord>();
        private readonly Dictionary<TickTaskNode, NodeRecord> nodes = new Dictionary<TickTaskNode, NodeRecord>();
        private readonly List<NodeRecord> pendingAdditions = new List<NodeRecord>();
        private readonly List<OwnerRecord> pendingRemovals = new List<OwnerRecord>();
        private readonly List<TickTaskFault> faults = new List<TickTaskFault>();
        private readonly List<TickTaskFault> domainFaults = new List<TickTaskFault>();
        private long nextRegistrationId;
        private bool isExecuting;

        public event Action<TickTaskFault> Faulted;

        public event Action<TickTaskFault> OwnerFaulted;

        public IReadOnlyList<TickTaskFault> Faults => faults;

        public IDisposable RegisterOwner(object owner, IEnumerable<TickTaskNode> ownerNodes, bool critical = false)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (ownerNodes == null)
            {
                throw new ArgumentNullException(nameof(ownerNodes));
            }

            if (owners.ContainsKey(owner))
            {
                throw new InvalidOperationException("Tick owner is already registered.");
            }

            var record = new OwnerRecord { Owner = owner, Critical = critical };
            var batch = new List<TickTaskNode>();
            foreach (TickTaskNode node in ownerNodes)
            {
                if (node == null)
                {
                    throw new InvalidOperationException("Tick owner contains a null node.");
                }

                if (nodes.ContainsKey(node) || batch.Contains(node))
                {
                    throw new InvalidOperationException($"TickTaskNode {node.Name} is already registered.");
                }

                batch.Add(node);
            }

            ValidatePrerequisites(batch);
            owners.Add(owner, record);
            for (int index = 0; index < batch.Count; index++)
            {
                TickTaskNode node = batch[index];
                node.RegistrationId = ++nextRegistrationId;
                node.IsRegistered = true;
                node.StructureFrozen = true;
                var nodeRecord = new NodeRecord
                {
                    Node = node,
                    Owner = record,
                    Committed = !isExecuting
                };
                nodes.Add(node, nodeRecord);
                record.Nodes.Add(nodeRecord);
                if (isExecuting)
                {
                    pendingAdditions.Add(nodeRecord);
                }
            }

            return new OwnerRegistration(this, owner);
        }

        public void SetOwnerEnabled(object owner, bool enabled)
        {
            OwnerRecord record = GetActiveOwner(owner);
            if (record.Faulted && enabled)
            {
                throw new InvalidOperationException("A faulted Tick owner cannot be re-enabled.");
            }

            if (record.Enabled == enabled)
            {
                return;
            }

            record.Enabled = enabled;
            ResetTimers(record);
        }

        public void ExecuteDomain(TickDomain domain, float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (isExecuting)
            {
                throw new InvalidOperationException("TickTaskManager does not allow recursive domain execution.");
            }

            CommitMutations();
            domainFaults.Clear();
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
                TickTaskFault[] completedFaults = domainFaults.ToArray();
                domainFaults.Clear();
                for (int index = 0; index < completedFaults.Length; index++)
                {
                    OwnerFaulted?.Invoke(completedFaults[index]);
                }

                CommitMutations();
            }
        }

        private void ExecuteGroup(TickGroup group, float deltaTime)
        {
            List<NodeRecord> ordered = BuildExecutionOrder(group);
            var unavailable = new HashSet<TickTaskNode>();
            for (int index = 0; index < ordered.Count; index++)
            {
                NodeRecord record = ordered[index];
                TickTaskNode node = record.Node;
                if (!CanExecute(record) || HasUnavailablePrerequisite(node, unavailable))
                {
                    unavailable.Add(node);
                    continue;
                }

                if (!node.Timer.Advance(deltaTime, node.TickInterval, out float elapsedTime))
                {
                    continue;
                }

                try
                {
                    node.Callback(elapsedTime);
                }
                catch (Exception exception)
                {
                    record.Owner.Faulted = true;
                    record.Owner.Enabled = false;
                    ResetTimers(record.Owner);
                    unavailable.Add(node);
                    var fault = new TickTaskFault(record.Owner.Owner, node, exception, record.Owner.Critical);
                    faults.Add(fault);
                    domainFaults.Add(fault);
                    Faulted?.Invoke(fault);
                }
            }
        }

        private List<NodeRecord> BuildExecutionOrder(TickGroup group)
        {
            var candidates = new List<NodeRecord>();
            foreach (NodeRecord record in nodes.Values)
            {
                if (record.Active && record.Committed && record.Node.Group == group)
                {
                    candidates.Add(record);
                }
            }

            candidates.Sort((left, right) => left.Node.RegistrationId.CompareTo(right.Node.RegistrationId));
            var result = new List<NodeRecord>(candidates.Count);
            var emitted = new HashSet<TickTaskNode>();
            while (result.Count < candidates.Count)
            {
                NodeRecord next = null;
                for (int index = 0; index < candidates.Count; index++)
                {
                    NodeRecord candidate = candidates[index];
                    if (!emitted.Contains(candidate.Node) && GroupPrerequisitesEmitted(candidate.Node, emitted))
                    {
                        next = candidate;
                        break;
                    }
                }

                if (next == null)
                {
                    throw new InvalidOperationException($"Tick prerequisite cycle detected in {group}.");
                }

                emitted.Add(next.Node);
                result.Add(next);
            }

            return result;
        }

        private void ValidatePrerequisites(List<TickTaskNode> batch)
        {
            var available = new HashSet<TickTaskNode>(nodes.Keys);
            for (int index = 0; index < batch.Count; index++)
            {
                available.Add(batch[index]);
            }

            for (int index = 0; index < batch.Count; index++)
            {
                TickTaskNode node = batch[index];
                for (int prerequisiteIndex = 0; prerequisiteIndex < node.Prerequisites.Count; prerequisiteIndex++)
                {
                    TickTaskNode prerequisite = node.Prerequisites[prerequisiteIndex];
                    if (!available.Contains(prerequisite))
                    {
                        throw new InvalidOperationException($"Tick prerequisite {prerequisite.Name} is not registered.");
                    }

                    if (node.Group != prerequisite.Group)
                    {
                        TickDomain nodeDomain = TickGroupUtility.GetDomain(node.Group);
                        TickDomain prerequisiteDomain = TickGroupUtility.GetDomain(prerequisite.Group);
                        string reason = nodeDomain == prerequisiteDomain
                            ? "Prerequisites must be within the same TickGroup."
                            : "Cross-domain prerequisites are not allowed.";
                        throw new InvalidOperationException(reason);
                    }
                }

                if (DependsOn(node, node, new HashSet<TickTaskNode>()))
                {
                    throw new InvalidOperationException("Tick prerequisite would create a cycle.");
                }
            }
        }

        private static bool DependsOn(TickTaskNode current, TickTaskNode target, HashSet<TickTaskNode> visited)
        {
            if (!visited.Add(current))
            {
                return false;
            }

            for (int index = 0; index < current.Prerequisites.Count; index++)
            {
                TickTaskNode prerequisite = current.Prerequisites[index];
                if (ReferenceEquals(prerequisite, target) || DependsOn(prerequisite, target, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanExecute(NodeRecord record)
        {
            return record.Active
                && record.Committed
                && record.Owner.Active
                && record.Owner.Enabled
                && !record.Owner.Faulted
                && record.Node.Enabled;
        }

        private bool HasUnavailablePrerequisite(TickTaskNode node, HashSet<TickTaskNode> unavailable)
        {
            for (int index = 0; index < node.Prerequisites.Count; index++)
            {
                TickTaskNode prerequisite = node.Prerequisites[index];
                if (unavailable.Contains(prerequisite)
                    || !nodes.TryGetValue(prerequisite, out NodeRecord record)
                    || !CanExecute(record))
                {
                    return true;
                }
            }

            return false;
        }

        private bool GroupPrerequisitesEmitted(TickTaskNode node, HashSet<TickTaskNode> emitted)
        {
            for (int index = 0; index < node.Prerequisites.Count; index++)
            {
                TickTaskNode prerequisite = node.Prerequisites[index];
                if (nodes.TryGetValue(prerequisite, out NodeRecord record)
                    && record.Active
                    && record.Committed
                    && !emitted.Contains(prerequisite))
                {
                    return false;
                }
            }

            return true;
        }

        private OwnerRecord GetActiveOwner(object owner)
        {
            if (owner == null || !owners.TryGetValue(owner, out OwnerRecord record) || !record.Active)
            {
                throw new InvalidOperationException("Tick owner is not registered.");
            }

            return record;
        }

        private void UnregisterOwner(object owner)
        {
            if (owner == null || !owners.TryGetValue(owner, out OwnerRecord record) || !record.Active)
            {
                return;
            }

            record.Active = false;
            record.Enabled = false;
            ResetTimers(record);
            for (int index = 0; index < record.Nodes.Count; index++)
            {
                record.Nodes[index].Active = false;
                record.Nodes[index].Node.IsRegistered = false;
            }

            pendingRemovals.Add(record);
            if (!isExecuting)
            {
                CommitMutations();
            }
        }

        private void CommitMutations()
        {
            for (int ownerIndex = 0; ownerIndex < pendingRemovals.Count; ownerIndex++)
            {
                OwnerRecord owner = pendingRemovals[ownerIndex];
                owners.Remove(owner.Owner);
                for (int nodeIndex = 0; nodeIndex < owner.Nodes.Count; nodeIndex++)
                {
                    nodes.Remove(owner.Nodes[nodeIndex].Node);
                    pendingAdditions.Remove(owner.Nodes[nodeIndex]);
                }
            }

            pendingRemovals.Clear();
            for (int index = 0; index < pendingAdditions.Count; index++)
            {
                if (pendingAdditions[index].Active)
                {
                    pendingAdditions[index].Committed = true;
                }
            }

            pendingAdditions.Clear();
        }

        private static void ResetTimers(OwnerRecord owner)
        {
            for (int index = 0; index < owner.Nodes.Count; index++)
            {
                owner.Nodes[index].Node.Timer.Reset();
            }
        }

        private static IEnumerable<TickGroup> GetGroups(TickDomain domain)
        {
            switch (domain)
            {
                case TickDomain.Fixed:
                    yield return TickGroup.TG_PrePhysics;
                    yield return TickGroup.TG_PhysicsMovement;
                    break;
                case TickDomain.Update:
                    yield return TickGroup.TG_PostPhysics;
                    yield return TickGroup.TG_Input;
                    yield return TickGroup.TG_Gameplay;
                    yield return TickGroup.TG_PreAnimation;
                    break;
                case TickDomain.Late:
                    yield return TickGroup.TG_PostAnimation;
                    yield return TickGroup.TG_Camera;
                    yield return TickGroup.TG_LatePresentation;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(domain), domain, null);
            }
        }
    }
}
