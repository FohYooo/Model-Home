using UnityEngine;

namespace ModelHome
{
    public static class FlashlightMath
    {
        /// <summary>True if point lies inside the cone (apex origin, axis forward, half-angle, range).</summary>
        public static bool IsInCone(Vector3 origin, Vector3 forward, float halfAngleDeg, float range, Vector3 point)
        {
            var d = point - origin;
            float dist = d.magnitude;
            if (dist < 1e-4f) return true;
            if (dist > range) return false;
            return Vector3.Angle(forward, d) <= halfAngleDeg;
        }
    }
}
