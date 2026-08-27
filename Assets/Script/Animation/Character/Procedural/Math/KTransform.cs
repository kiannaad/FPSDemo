using System;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public struct KTransform
    {
        public static readonly KTransform Identity = new KTransform(Vector3.zero, Quaternion.identity, Vector3.one);

        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public KTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public KTransform(Vector3 position, Quaternion rotation)
            : this(position, rotation, Vector3.one)
        {
        }

        public KTransform(Transform transform, bool worldSpace = true)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            Position = worldSpace ? transform.position : transform.localPosition;
            Rotation = worldSpace ? transform.rotation : transform.localRotation;
            Scale = transform.localScale;
        }

        public static KTransform Lerp(KTransform from, KTransform to, float alpha)
        {
            return new KTransform(
                Vector3.Lerp(from.Position, to.Position, alpha),
                Quaternion.Slerp(from.Rotation, to.Rotation, alpha),
                Vector3.Lerp(from.Scale, to.Scale, alpha));
        }

        public Vector3 InverseTransformPoint(Vector3 worldPosition, bool useScale)
        {
            Vector3 result = Quaternion.Inverse(Rotation) * (worldPosition - Position);
            return useScale ? Vector3.Scale(Scale, result) : result;
        }

        public Vector3 TransformPoint(Vector3 localPosition, bool useScale)
        {
            Vector3 scaled = useScale ? Vector3.Scale(Scale, localPosition) : localPosition;
            return Position + Rotation * scaled;
        }

        public KTransform GetRelativeTransform(KTransform worldTransform, bool useScale)
        {
            return new KTransform(
                InverseTransformPoint(worldTransform.Position, useScale),
                Quaternion.Inverse(Rotation) * worldTransform.Rotation,
                Vector3.Scale(Scale, worldTransform.Scale));
        }

        public KTransform GetWorldTransform(KTransform localTransform, bool useScale)
        {
            return new KTransform(
                TransformPoint(localTransform.Position, useScale),
                Rotation * localTransform.Rotation,
                Vector3.Scale(Scale, localTransform.Scale));
        }

        public bool Equals(KTransform other, bool useScale)
        {
            return Position.Equals(other.Position)
                && Rotation.Equals(other.Rotation)
                && (!useScale || Scale.Equals(other.Scale));
        }
    }
}
