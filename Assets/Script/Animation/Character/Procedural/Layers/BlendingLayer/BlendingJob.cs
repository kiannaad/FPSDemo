using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct BlendingJobAtom
    {
        public TransformStreamHandle Handle;
        public KTransform ActivePose;
        public KTransform DesiredComponentPose;
        public float ElementWeight;
    }

    public struct BlendingJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public NativeArray<BlendingJobAtom> Elements;
        public float Weight;
        public bool BlendPosition;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;

            KTransform rootPose = AnimationLayerJobUtility.GetTransform(stream, Root);
            for (int index = 0; index < Elements.Length; index++)
            {
                BlendingJobAtom atom = Elements[index];
                atom.ActivePose = AnimationLayerJobUtility.GetTransform(stream, atom.Handle);
                KTransform desiredPose = rootPose.GetWorldTransform(atom.DesiredComponentPose, false);
                float blendWeight = Mathf.Clamp01(Weight * atom.ElementWeight);
                if (BlendPosition)
                {
                    atom.Handle.SetPosition(
                        stream,
                        Vector3.Lerp(atom.ActivePose.Position, desiredPose.Position, blendWeight));
                }

                atom.Handle.SetRotation(
                    stream,
                    Quaternion.Slerp(atom.ActivePose.Rotation, desiredPose.Rotation, blendWeight));
                Elements[index] = atom;
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
