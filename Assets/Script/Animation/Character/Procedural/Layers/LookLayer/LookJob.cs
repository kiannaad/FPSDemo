using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct LookJobAtom
    {
        public TransformStreamHandle Handle;
        public Vector2 AngleLimits;
    }

    public struct LookJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public NativeArray<LookJobAtom> Pitch;
        public NativeArray<LookJobAtom> Yaw;
        public NativeArray<LookJobAtom> Roll;
        public Vector2 ViewAnglesDegrees;
        public float LeanAngleDegrees;
        public float Weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            ApplyInRootSpace(stream, Roll, LeanAngleDegrees, Vector3.forward);
            ApplyInRootSpace(stream, Yaw, ViewAnglesDegrees.x, Vector3.up);
            Quaternion pitchSpace = Root.GetRotation(stream)
                * Quaternion.AngleAxis(ViewAnglesDegrees.x, Vector3.up);
            ApplyInSpace(stream, Pitch, ViewAnglesDegrees.y, Vector3.right, pitchSpace);
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        private void ApplyInRootSpace(AnimationStream stream, NativeArray<LookJobAtom> elements, float input, Vector3 axis)
        {
            float fraction = Mathf.Clamp(input / 90f, -1f, 1f);
            for (int index = 0; index < elements.Length; index++)
            {
                LookJobAtom atom = elements[index];
                float limit = fraction >= 0f ? atom.AngleLimits.x : atom.AngleLimits.y;
                AnimationLayerJobUtility.ModifyTransform(
                    stream,
                    Root,
                    atom.Handle,
                    new KPose
                    {
                        Pose = new KTransform(Vector3.zero, Quaternion.AngleAxis(limit * fraction, axis)),
                        Space = TransformSpace.ComponentSpace,
                        ModifyMode = TransformModifyMode.Add
                    },
                    Weight);
            }
        }

        private void ApplyInSpace(
            AnimationStream stream,
            NativeArray<LookJobAtom> elements,
            float input,
            Vector3 axis,
            Quaternion space)
        {
            float fraction = Mathf.Clamp(input / 90f, -1f, 1f);
            for (int index = 0; index < elements.Length; index++)
            {
                LookJobAtom atom = elements[index];
                float limit = fraction >= 0f ? atom.AngleLimits.x : atom.AngleLimits.y;
                Quaternion current = atom.Handle.GetRotation(stream);
                Quaternion offset = Quaternion.AngleAxis(limit * fraction, axis);
                Quaternion desired = space * offset * Quaternion.Inverse(space) * current;
                atom.Handle.SetRotation(stream, Quaternion.Slerp(current, desired, Weight));
            }
        }
    }
}
