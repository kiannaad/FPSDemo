using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation.Rig
{
    public sealed class KRigComponent : MonoBehaviour
    {
        [SerializeField] private List<Transform> hierarchy = new List<Transform>();
        [SerializeField] private List<int> hierarchyDepths = new List<int>();

        private KRig rig;
        private bool isInitialized;
        private KTransform[] initializedLocalPose;

        public KRig Rig => rig;
        public bool IsInitialized => isInitialized;

        public void Initialize(KRig expectedRig)
        {
            if (expectedRig == null)
            {
                throw new ArgumentNullException(nameof(expectedRig));
            }

            ValidateHierarchy(expectedRig);
            ValidateVirtualElements();
            rig = expectedRig;
            initializedLocalPose = new KTransform[hierarchy.Count];
            for (int index = 0; index < hierarchy.Count; index++)
            {
                initializedLocalPose[index] = new KTransform(hierarchy[index], false);
            }
            isInitialized = true;
        }

        public Transform GetRigTransform(KRigElement element)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("KRigComponent must be initialized before resolving rig transforms.");
            }

            if (element.Index < 0 || element.Index >= hierarchy.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(element));
            }

            return hierarchy[element.Index];
        }

        public Transform GetRigTransform(int index)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("KRigComponent must be initialized before resolving rig transforms.");
            }

            if (index < 0 || index >= hierarchy.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return hierarchy[index];
        }

        public void RestoreInitializedHierarchyPose()
        {
            if (!isInitialized || initializedLocalPose == null || initializedLocalPose.Length != hierarchy.Count)
            {
                throw new InvalidOperationException("KRigComponent has no initialized hierarchy pose.");
            }

            for (int index = 0; index < hierarchy.Count; index++)
            {
                KTransform pose = initializedLocalPose[index];
                Transform target = hierarchy[index];
                target.localPosition = pose.Position;
                target.localRotation = pose.Rotation;
                target.localScale = pose.Scale;
            }
        }

#if UNITY_EDITOR
        public void RefreshHierarchy()
        {
            hierarchy.Clear();
            hierarchyDepths.Clear();
            AddHierarchy(transform, 0);
        }

        public Transform[] GetSerializedHierarchy()
        {
            return hierarchy.ToArray();
        }

        public int GetHierarchyDepth(int index)
        {
            return hierarchyDepths[index];
        }

        private void AddHierarchy(Transform current, int depth)
        {
            hierarchy.Add(current);
            hierarchyDepths.Add(depth);
            foreach (Transform child in current)
            {
                AddHierarchy(child, depth + 1);
            }
        }
#endif

        private void ValidateHierarchy(KRig expectedRig)
        {
            IReadOnlyList<KRigElement> expectedHierarchy = expectedRig.Hierarchy;
            if (hierarchy == null || hierarchy.Count != expectedHierarchy.Count)
            {
                throw new InvalidOperationException("KRig hierarchy count does not match the expected asset.");
            }

            for (int index = 0; index < hierarchy.Count; index++)
            {
                Transform actual = hierarchy[index];
                KRigElement expected = expectedHierarchy[index];
                if (actual == null)
                {
                    throw new InvalidOperationException($"KRig hierarchy entry {index} is null.");
                }

                if (expected.Index != index)
                {
                    throw new InvalidOperationException($"KRig asset entry {index} has an invalid index.");
                }

                if (!string.Equals(expected.Name, actual.name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"KRig hierarchy entry {index} does not match the expected name.");
                }
            }
        }

        private void ValidateVirtualElements()
        {
            HashSet<Transform> transforms = new HashSet<Transform>(hierarchy);
            KVirtualElement[] virtualElements = GetComponentsInChildren<KVirtualElement>(true);
            foreach (KVirtualElement virtualElement in virtualElements)
            {
                virtualElement.Validate(transforms);
            }
        }
    }
}
