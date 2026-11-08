using UnityEngine;

namespace ModelHome
{
    public static class GazeMath
    {
        /// <summary>Limits a desired look direction to within maxAngleDeg of the rest direction, so the eyes
        /// never roll back into the head when the player is behind or above the figure.</summary>
        public static Vector3 ClampToCone(Vector3 restForward, Vector3 desired, float maxAngleDeg)
        {
            var rest = restForward.normalized;
            var want = desired.sqrMagnitude > 1e-8f ? desired.normalized : rest;
            float angle = Vector3.Angle(rest, want);
            if (angle <= maxAngleDeg) return want;
            if (angle > 179.9f)
            {
                var axis = Vector3.Cross(rest, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(rest, Vector3.right);
                return (Quaternion.AngleAxis(maxAngleDeg, axis.normalized) * rest).normalized;
            }
            return Vector3.RotateTowards(rest, want, maxAngleDeg * Mathf.Deg2Rad, 0f).normalized;
        }
    }
}
