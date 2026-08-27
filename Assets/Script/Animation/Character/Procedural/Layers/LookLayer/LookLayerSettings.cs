using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public struct LookLayerElement
    {
        public KRigElement Element;
        public Vector2 AngleLimits;
    }

    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Look Layer", fileName = "LookLayerSettings")]
    public sealed class LookLayerSettings : AnimationLayerSettings
    {
        [SerializeField] private bool useTurnOffset;
        [SerializeField] private List<LookLayerElement> pitchElements = new List<LookLayerElement>();
        [SerializeField] private List<LookLayerElement> yawElements = new List<LookLayerElement>();
        [SerializeField] private List<LookLayerElement> rollElements = new List<LookLayerElement>();

        public IReadOnlyList<LookLayerElement> PitchElements => pitchElements;
        public IReadOnlyList<LookLayerElement> YawElements => yawElements;
        public IReadOnlyList<LookLayerElement> RollElements => rollElements;
        public bool UseTurnOffset => useTurnOffset;
        public override IAnimationLayerJob CreateAnimationJob() => new LookLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            ValidateCollection(expectedRig, pitchElements, "pitch");
            ValidateCollection(expectedRig, yawElements, "yaw");
            ValidateCollection(expectedRig, rollElements, "roll");
        }

        protected override void OnRigUpdated()
        {
            Synchronize(pitchElements);
            Synchronize(yawElements);
            Synchronize(rollElements);
        }

        private void ValidateCollection(KRig rig, List<LookLayerElement> elements, string label)
        {
            if (elements == null) throw new InvalidOperationException(name + " " + label + " elements are missing.");
            HashSet<int> indices = new HashSet<int>();
            for (int index = 0; index < elements.Count; index++)
            {
                KRigElement resolved = RigHandleUtility.ResolveElement(rig, elements[index].Element, name + " " + label);
                if (!indices.Add(resolved.Index))
                {
                    throw new InvalidOperationException(name + " duplicates " + label + " bone " + resolved.Name + ".");
                }
            }
        }

        private void Synchronize(List<LookLayerElement> elements)
        {
            for (int index = 0; index < elements.Count; index++)
            {
                LookLayerElement entry = elements[index];
                SynchronizeRigElement(ref entry.Element);
                elements[index] = entry;
            }
        }
    }
}
