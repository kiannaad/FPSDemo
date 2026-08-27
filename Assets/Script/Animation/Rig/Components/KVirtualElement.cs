using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation.Rig
{
    public sealed class KVirtualElement : MonoBehaviour
    {
        [SerializeField] private Transform targetBone;

        public Transform TargetBone => targetBone;

        internal void Validate(ISet<Transform> hierarchy)
        {
            if (targetBone == null)
            {
                throw new InvalidOperationException($"Virtual element '{name}' has no target bone.");
            }

            if (targetBone == transform)
            {
                throw new InvalidOperationException($"Virtual element '{name}' cannot target itself.");
            }

            if (!hierarchy.Contains(targetBone))
            {
                throw new InvalidOperationException(
                    $"Virtual element '{name}' targets a Transform outside its KRig hierarchy.");
            }
        }
    }
}
