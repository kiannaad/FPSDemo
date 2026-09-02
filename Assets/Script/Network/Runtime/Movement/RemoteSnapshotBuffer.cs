using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public sealed class RemoteSnapshotBuffer
    {
        private readonly List<AuthoritySnapshot> snapshots = new List<AuthoritySnapshot>();

        public int Count => snapshots.Count;
        public long LatestServerTick => snapshots.Count == 0 ? 0 : snapshots[snapshots.Count - 1].State.ServerTick;

        public bool Add(AuthoritySnapshot snapshot)
        {
            if (snapshots.Count > 0 && snapshot.State.ServerTick <= LatestServerTick) return false;
            snapshots.Add(snapshot);
            if (snapshots.Count > 32) snapshots.RemoveAt(0);
            return true;
        }

        public bool TrySample(long serverTick, out AuthorityState state)
        {
            state = default;
            if (snapshots.Count == 0) return false;
            if (snapshots.Count == 1 || serverTick <= snapshots[0].State.ServerTick)
            {
                state = snapshots[0].State;
                return true;
            }

            for (int index = 1; index < snapshots.Count; index++)
            {
                AuthorityState newer = snapshots[index].State;
                if (serverTick > newer.ServerTick) continue;
                AuthorityState older = snapshots[index - 1].State;
                float alpha = Mathf.InverseLerp(older.ServerTick, newer.ServerTick, serverTick);
                state = Interpolate(older, newer, serverTick, alpha);
                return true;
            }

            AuthorityState latest = snapshots[snapshots.Count - 1].State;
            float elapsedSeconds = Mathf.Min(
                (serverTick - latest.ServerTick) / (float)NetworkTickClock.TicksPerSecond,
                0.1f);
            Vector3 extrapolatedPosition = latest.Position.ToMeters() + latest.BaseVelocity.ToMeters() * elapsedSeconds;
            state = new AuthorityState(
                serverTick,
                QuantizedVector3.FromMeters(extrapolatedPosition),
                latest.Rotation,
                latest.BaseVelocity,
                latest.MovementState,
                latest.Grounded,
                latest.GroundNormal,
                latest.AttachedBaseId,
                latest.ControlRotation,
                latest.IsAiming);
            return true;
        }

        private static AuthorityState Interpolate(AuthorityState older, AuthorityState newer, long serverTick, float alpha)
        {
            Vector3 position = Vector3.Lerp(older.Position.ToMeters(), newer.Position.ToMeters(), alpha);
            Vector3 velocity = Vector3.Lerp(older.BaseVelocity.ToMeters(), newer.BaseVelocity.ToMeters(), alpha);
            Vector3 normal = Vector3.Lerp(older.GroundNormal.ToMeters(), newer.GroundNormal.ToMeters(), alpha).normalized;
            Quaternion rotation = Quaternion.Slerp(older.Rotation.ToQuaternion(), newer.Rotation.ToQuaternion(), alpha);
            Quaternion controlRotation = Quaternion.Slerp(
                older.ControlRotation.ToQuaternion(),
                newer.ControlRotation.ToQuaternion(),
                alpha);
            AuthorityState discrete = alpha < 1f ? older : newer;
            return new AuthorityState(
                serverTick,
                QuantizedVector3.FromMeters(position),
                QuantizedQuaternion.FromQuaternion(rotation),
                QuantizedVector3.FromMeters(velocity),
                discrete.MovementState,
                discrete.Grounded,
                QuantizedVector3.FromMeters(normal),
                discrete.AttachedBaseId,
                QuantizedQuaternion.FromQuaternion(controlRotation),
                discrete.IsAiming);
        }
    }

    public static class AuthorityStateDigest
    {
        public static bool ExactEquals(AuthorityState left, AuthorityState right, out string mismatch)
        {
            if (!left.Position.Equals(right.Position)) return Fail("Position", out mismatch);
            if (!left.Rotation.Equals(right.Rotation)) return Fail("Rotation", out mismatch);
            if (!left.BaseVelocity.Equals(right.BaseVelocity)) return Fail("BaseVelocity", out mismatch);
            if (left.MovementState != right.MovementState) return Fail("MovementState", out mismatch);
            if (left.Grounded != right.Grounded) return Fail("Grounded", out mismatch);
            if (!left.GroundNormal.Equals(right.GroundNormal)) return Fail("GroundNormal", out mismatch);
            if (left.AttachedBaseId != right.AttachedBaseId) return Fail("AttachedBaseId", out mismatch);
            if (!left.ControlRotation.Equals(right.ControlRotation)) return Fail("ControlRotation", out mismatch);
            if (left.IsAiming != right.IsAiming) return Fail("IsAiming", out mismatch);
            mismatch = string.Empty;
            return true;
        }

        private static bool Fail(string field, out string mismatch)
        {
            mismatch = field;
            return false;
        }
    }
}
