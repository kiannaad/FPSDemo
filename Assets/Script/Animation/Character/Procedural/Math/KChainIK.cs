using UnityEngine;

namespace CGame.Animation
{
    public struct ChainIkData
    {
        public Vector3[] Positions;
        public float[] Lengths;
        public Vector3 Target;
        public float Tolerance;
        public float MaxReach;
        public int MaxIterations;
    }

    public static class KChainIK
    {
        public static bool SolveFabrik(ref ChainIkData data)
        {
            Validate(data);
            Vector3 rootToTarget = data.Target - data.Positions[0];
            if (rootToTarget.sqrMagnitude > data.MaxReach * data.MaxReach)
            {
                Vector3 direction = rootToTarget.normalized;
                for (int index = 1; index < data.Positions.Length; index++)
                {
                    data.Positions[index] = data.Positions[index - 1] + direction * data.Lengths[index - 1];
                }

                return true;
            }

            int tipIndex = data.Positions.Length - 1;
            float squaredTolerance = data.Tolerance * data.Tolerance;
            if ((data.Positions[tipIndex] - data.Target).sqrMagnitude <= squaredTolerance)
            {
                return false;
            }

            Vector3 rootPosition = data.Positions[0];
            int iteration = 0;
            do
            {
                data.Positions[tipIndex] = data.Target;
                for (int index = tipIndex - 1; index >= 0; index--)
                {
                    data.Positions[index] = data.Positions[index + 1]
                        + (data.Positions[index] - data.Positions[index + 1]).normalized * data.Lengths[index];
                }

                data.Positions[0] = rootPosition;
                for (int index = 1; index < data.Positions.Length; index++)
                {
                    data.Positions[index] = data.Positions[index - 1]
                        + (data.Positions[index] - data.Positions[index - 1]).normalized * data.Lengths[index - 1];
                }
            }
            while ((data.Positions[tipIndex] - data.Target).sqrMagnitude > squaredTolerance
                   && ++iteration < data.MaxIterations);

            return true;
        }

        private static void Validate(ChainIkData data)
        {
            if (data.Positions == null || data.Positions.Length < 2)
            {
                throw new System.ArgumentException("A chain requires at least two positions.");
            }

            if (data.Lengths == null || data.Lengths.Length != data.Positions.Length - 1)
            {
                throw new System.ArgumentException("Chain lengths must describe every adjacent position pair.");
            }

            if (data.Tolerance < 0f || data.MaxReach < 0f || data.MaxIterations < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(data));
            }
        }
    }
}
