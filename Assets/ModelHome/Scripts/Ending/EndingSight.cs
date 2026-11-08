using UnityEngine;

namespace ModelHome
{
    public static class EndingSight
    {
        /// <summary>True when a point (the Daughter's eye midpoint) projects well inside the view, not just
        /// clipping an edge: in front of the camera, 0.1-0.9 across and 0.15-0.9 up the viewport.</summary>
        public static bool EyesInView(Camera cam, Vector3 point)
        {
            var v = cam.WorldToViewportPoint(point);
            return v.z > 0.05f && v.x > 0.1f && v.x < 0.9f && v.y > 0.15f && v.y < 0.9f;
        }
    }
}
