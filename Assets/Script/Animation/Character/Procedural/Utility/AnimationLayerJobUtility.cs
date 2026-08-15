using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public static class AnimationLayerJobUtility
    {
        public static KTransform GetTransform(
            AnimationStream stream,
            TransformStreamHandle handle,
            bool worldSpace = true)
        {
            return new KTransform(
                worldSpace ? handle.GetPosition(stream) : handle.GetLocalPosition(stream),
                worldSpace ? handle.GetRotation(stream) : handle.GetLocalRotation(stream));
        }

        public static void ModifyTransform(
            AnimationStream stream,
            TransformStreamHandle root,
            TransformStreamHandle target,
            KPose pose,
            float weight)
        {
            if (pose.ModifyMode == TransformModifyMode.Ignore || !KCurves.IsWeightRelevant(weight)) return;

            KTransform rootTransform = GetTransform(stream, root);
            if (pose.ModifyMode == TransformModifyMode.Add)
            {
                if (pose.Space == TransformSpace.BoneSpace)
                {
                    MoveInSpace(stream, target, target, pose.Pose.Position, weight);
                    RotateInSpace(stream, target, target, pose.Pose.Rotation, weight);
                    return;
                }

                if (pose.Space == TransformSpace.ParentBoneSpace)
                {
                    KTransform local = GetTransform(stream, target, false);
                    target.SetLocalPosition(stream, Vector3.Lerp(
                        local.Position,
                        local.Position + pose.Pose.Position,
                        weight));
                    target.SetLocalRotation(stream, Quaternion.Slerp(
                        local.Rotation,
                        local.Rotation * pose.Pose.Rotation,
                        weight));
                    return;
                }

                if (pose.Space == TransformSpace.ComponentSpace)
                {
                    MoveInSpace(stream, root, target, pose.Pose.Position, weight);
                    RotateInSpace(stream, root, target, pose.Pose.Rotation, weight);
                    return;
                }

                KTransform world = GetTransform(stream, target);
                target.SetPosition(stream, Vector3.Lerp(
                    world.Position,
                    world.Position + pose.Pose.Position,
                    weight));
                target.SetRotation(stream, Quaternion.Slerp(
                    world.Rotation,
                    world.Rotation * pose.Pose.Rotation,
                    weight));
                return;
            }

            if (pose.Space == TransformSpace.BoneSpace || pose.Space == TransformSpace.ParentBoneSpace)
            {
                target.SetLocalPosition(stream, Vector3.Lerp(
                    target.GetLocalPosition(stream),
                    pose.Pose.Position,
                    weight));
                target.SetLocalRotation(stream, Quaternion.Slerp(
                    target.GetLocalRotation(stream),
                    pose.Pose.Rotation,
                    weight));
                return;
            }

            if (pose.Space == TransformSpace.ComponentSpace)
            {
                KTransform desired = rootTransform.GetWorldTransform(pose.Pose, false);
                KTransform blended = KTransform.Lerp(GetTransform(stream, target), desired, weight);
                target.SetPosition(stream, blended.Position);
                target.SetRotation(stream, blended.Rotation);
                return;
            }

            target.SetPosition(stream, Vector3.Lerp(
                target.GetPosition(stream),
                pose.Pose.Position,
                weight));
            target.SetRotation(stream, Quaternion.Slerp(
                target.GetRotation(stream),
                pose.Pose.Rotation,
                weight));
        }

        private static void MoveInSpace(
            AnimationStream stream,
            TransformStreamHandle space,
            TransformStreamHandle target,
            Vector3 offset,
            float weight)
        {
            KTransform spaceTransform = GetTransform(stream, space);
            Vector3 desired = target.GetPosition(stream) + spaceTransform.Rotation * offset;
            target.SetPosition(stream, Vector3.Lerp(target.GetPosition(stream), desired, weight));
        }

        private static void RotateInSpace(
            AnimationStream stream,
            TransformStreamHandle space,
            TransformStreamHandle target,
            Quaternion offset,
            float weight)
        {
            Quaternion spaceRotation = space.GetRotation(stream);
            Quaternion targetRotation = target.GetRotation(stream);
            Quaternion relative = Quaternion.Inverse(spaceRotation) * targetRotation;
            Quaternion desired = spaceRotation * (offset * relative);
            target.SetRotation(stream, Quaternion.Slerp(targetRotation, desired, weight));
        }
    }
}
