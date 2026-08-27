using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public static class RigHandleUtility
    {
        public static KRigElement ResolveElement(KRig rig, KRigElement element, string owner)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));
            if (string.IsNullOrWhiteSpace(element.Name))
            {
                throw new InvalidOperationException(owner + " has no rig element name.");
            }

            if (element.Index >= 0 && element.Index < rig.Hierarchy.Count)
            {
                KRigElement indexed = rig.Hierarchy[element.Index];
                if (string.Equals(indexed.Name, element.Name, StringComparison.Ordinal))
                {
                    return indexed;
                }
            }

            KRigElement? match = null;
            foreach (KRigElement candidate in rig.Hierarchy)
            {
                if (!string.Equals(candidate.Name, element.Name, StringComparison.Ordinal)) continue;
                if (match.HasValue)
                {
                    throw new InvalidOperationException(owner + " rig element name is duplicated: " + element.Name);
                }

                match = candidate;
            }

            if (!match.HasValue)
            {
                throw new InvalidOperationException(owner + " rig element is missing: " + element.Name);
            }

            if (element.Index >= 0 && element.Index != match.Value.Index)
            {
                throw new InvalidOperationException(owner + " rig element index does not match: " + element.Name);
            }

            return match.Value;
        }

        public static Transform ResolveTransform(KRigComponent component, KRigElement element, string owner)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            KRigElement resolved = ResolveElement(component.Rig, element, owner);
            Transform transform = component.GetRigTransform(resolved);
            if (!string.Equals(transform.name, resolved.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(owner + " resolved a mismatched transform: " + resolved.Name);
            }

            return transform;
        }

        public static TransformStreamHandle Bind(
            Animator animator,
            KRigComponent component,
            KRigElement element,
            string owner)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            return animator.BindStreamTransform(ResolveTransform(component, element, owner));
        }

        public static IReadOnlyList<KRigElement> ResolveChain(KRig rig, string chainName, string owner)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));
            if (string.IsNullOrWhiteSpace(chainName))
            {
                throw new InvalidOperationException(owner + " has no rig chain name.");
            }

            KRigElementChain match = null;
            foreach (KRigElementChain chain in rig.ElementChains)
            {
                if (!string.Equals(chain.Name, chainName, StringComparison.Ordinal)) continue;
                if (match != null)
                {
                    throw new InvalidOperationException(owner + " rig chain name is duplicated: " + chainName);
                }

                match = chain;
            }

            if (match == null || match.Elements == null || match.Elements.Count == 0)
            {
                throw new InvalidOperationException(owner + " rig chain is missing or empty: " + chainName);
            }

            HashSet<int> indices = new HashSet<int>();
            for (int index = 0; index < match.Elements.Count; index++)
            {
                KRigElement resolved = ResolveElement(rig, match.Elements[index], owner + " chain " + chainName);
                if (!indices.Add(resolved.Index))
                {
                    throw new InvalidOperationException(owner + " rig chain duplicates: " + resolved.Name);
                }
            }

            return match.Elements;
        }
    }
}
