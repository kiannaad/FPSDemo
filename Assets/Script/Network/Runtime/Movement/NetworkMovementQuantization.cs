using System;
using UnityEngine;

namespace CGame.Network
{
    [Serializable]
    public readonly struct QuantizedVector3 : IEquatable<QuantizedVector3>
    {
        public QuantizedVector3(int xMillimeters, int yMillimeters, int zMillimeters)
        {
            XMillimeters = xMillimeters;
            YMillimeters = yMillimeters;
            ZMillimeters = zMillimeters;
        }

        public int XMillimeters { get; }
        public int YMillimeters { get; }
        public int ZMillimeters { get; }

        public static QuantizedVector3 FromMeters(Vector3 value) => new QuantizedVector3(
            Quantize(value.x, 1000d),
            Quantize(value.y, 1000d),
            Quantize(value.z, 1000d));

        public Vector3 ToMeters() => new Vector3(
            XMillimeters / 1000f,
            YMillimeters / 1000f,
            ZMillimeters / 1000f);

        public bool Equals(QuantizedVector3 other) =>
            XMillimeters == other.XMillimeters &&
            YMillimeters == other.YMillimeters &&
            ZMillimeters == other.ZMillimeters;

        public override bool Equals(object obj) => obj is QuantizedVector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(XMillimeters, YMillimeters, ZMillimeters);

        private static int Quantize(float value, double scale) => checked((int)Math.Round(value * scale, MidpointRounding.AwayFromZero));
    }

    [Serializable]
    public readonly struct QuantizedInput : IEquatable<QuantizedInput>
    {
        public QuantizedInput(short x, short y)
        {
            X = x;
            Y = y;
        }

        public short X { get; }
        public short Y { get; }

        public static QuantizedInput FromVector2(Vector2 value) => new QuantizedInput(
            Quantize(value.x),
            Quantize(value.y));

        public Vector2 ToVector2() => new Vector2(X / (float)short.MaxValue, Y / (float)short.MaxValue);
        public bool Equals(QuantizedInput other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is QuantizedInput other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        private static short Quantize(float value)
        {
            float clamped = Mathf.Clamp(value, -1f, 1f);
            return checked((short)Math.Round(clamped * short.MaxValue, MidpointRounding.AwayFromZero));
        }
    }

    [Serializable]
    public readonly struct QuantizedView : IEquatable<QuantizedView>
    {
        public QuantizedView(int yawCentidegrees, int pitchCentidegrees)
        {
            YawCentidegrees = yawCentidegrees;
            PitchCentidegrees = pitchCentidegrees;
        }

        public int YawCentidegrees { get; }
        public int PitchCentidegrees { get; }
        public float YawDegrees => YawCentidegrees / 100f;
        public float PitchDegrees => PitchCentidegrees / 100f;

        public static QuantizedView FromDegrees(float yaw, float pitch) => new QuantizedView(
            checked((int)Math.Round(yaw * 100d, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(pitch * 100d, MidpointRounding.AwayFromZero)));

        public bool Equals(QuantizedView other) =>
            YawCentidegrees == other.YawCentidegrees && PitchCentidegrees == other.PitchCentidegrees;
        public override bool Equals(object obj) => obj is QuantizedView other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(YawCentidegrees, PitchCentidegrees);
    }

    [Serializable]
    public readonly struct QuantizedQuaternion : IEquatable<QuantizedQuaternion>
    {
        public QuantizedQuaternion(short x, short y, short z, short w)
        {
            X = x; Y = y; Z = z; W = w;
        }

        public short X { get; }
        public short Y { get; }
        public short Z { get; }
        public short W { get; }

        public static QuantizedQuaternion FromQuaternion(Quaternion value)
        {
            value = value.normalized;
            return new QuantizedQuaternion(Quantize(value.x), Quantize(value.y), Quantize(value.z), Quantize(value.w));
        }

        public Quaternion ToQuaternion() => new Quaternion(
            X / (float)short.MaxValue,
            Y / (float)short.MaxValue,
            Z / (float)short.MaxValue,
            W / (float)short.MaxValue).normalized;

        public bool Equals(QuantizedQuaternion other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
        public override bool Equals(object obj) => obj is QuantizedQuaternion other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        private static short Quantize(float value) => checked((short)Math.Round(Mathf.Clamp(value, -1f, 1f) * short.MaxValue, MidpointRounding.AwayFromZero));
    }
}
