using UnityEngine;

namespace ModelHome
{
    /// <summary>
    /// Facing convention for objects imported from the Blender generators (figure roots and house
    /// anchors). Blender builds them facing local -Y; Unity's FBX import keeps that local frame and puts
    /// a -90 deg X rotation on the root, so the facing direction is the transform's -up.
    /// Positions map Unity (x, y, z) = Blender (-x, z, -y): the plan's floor plan appears turned 180 deg.
    /// </summary>
    public static class Facing
    {
        public static Vector3 Of(Transform t) => -t.up;

        /// <summary>Facing projected onto the floor plane and normalised.</summary>
        public static Vector3 Flat(Transform t)
        {
            var f = Of(t);
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }
    }
}
