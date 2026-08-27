using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation.Rig
{
    [CreateAssetMenu(menuName = "CGame/Animation/Rig", fileName = "KRig")]
    public sealed class KRig : ScriptableObject, IRigProvider
    {
        [SerializeField] private RuntimeAnimatorController targetAnimator;
        [SerializeField] private List<KRigElement> hierarchy = new List<KRigElement>();
        [SerializeField] private List<KRigElementChain> elementChains = new List<KRigElementChain>();

        public RuntimeAnimatorController TargetAnimator => targetAnimator;
        public IReadOnlyList<KRigElement> Hierarchy => hierarchy;
        public IReadOnlyList<KRigElementChain> ElementChains => elementChains;

        public KRigElement[] GetHierarchy()
        {
            return hierarchy.ToArray();
        }

#if UNITY_EDITOR
        public void Import(KRigComponent rigComponent)
        {
            if (rigComponent == null)
            {
                throw new ArgumentNullException(nameof(rigComponent));
            }

            rigComponent.RefreshHierarchy();
            Transform[] transforms = rigComponent.GetSerializedHierarchy();
            hierarchy.Clear();
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform transform = transforms[index];
                hierarchy.Add(new KRigElement(
                    index,
                    transform.name,
                    rigComponent.GetHierarchyDepth(index)));
            }
        }
#endif
    }
}
