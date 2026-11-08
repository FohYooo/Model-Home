using UnityEngine;

namespace ModelHome
{
    public static class Visibility
    {
        static readonly Plane[] Planes = new Plane[6];

        /// <summary>True if the renderer is inside the camera frustum and at least one of its sample points
        /// (bounds centre, top and two upper corners) has a clear line of sight from the camera.</summary>
        public static bool IsSeen(Renderer r, Camera cam, int occluderMask = Physics.DefaultRaycastLayers)
        {
            if (r == null || cam == null) return false;
            GeometryUtility.CalculateFrustumPlanes(cam, Planes);
            var b = r.bounds;
            if (!GeometryUtility.TestPlanesAABB(Planes, b)) return false;
            var root = r.transform.root;
            var origin = cam.transform.position;
            var c = b.center;
            var e = b.extents;
            var samples = new[]
            {
                c, c + new Vector3(0, e.y * 0.8f, 0),
                c + new Vector3(e.x * 0.7f, e.y * 0.5f, 0), c + new Vector3(-e.x * 0.7f, e.y * 0.5f, 0),
            };
            foreach (var p in samples)
            {
                var d = p - origin;
                if (!Physics.Raycast(origin, d.normalized, out var hit, d.magnitude, occluderMask, QueryTriggerInteraction.Ignore))
                    return true;
                if (hit.transform.IsChildOf(root)) return true;
            }
            return false;
        }
    }
}
