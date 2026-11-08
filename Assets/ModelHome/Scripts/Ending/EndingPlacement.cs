using System.Collections.Generic;
using UnityEngine;

namespace ModelHome
{
    public static class EndingPlacement
    {
        /// <summary>Where the Daughter may stand behind the guard, best first: 0.6 m directly behind,
        /// 0.6 m at +/-45 deg behind, 0.45 m behind, then 0.45 m beside. All on the guard's floor height;
        /// look pitch is ignored. The caller picks the first one that is clear of geometry.</summary>
        public static IReadOnlyList<Vector3> Candidates(Vector3 playerFeet, Vector3 playerForward)
        {
            var fwd = new Vector3(playerForward.x, 0f, playerForward.z);
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
            var back = -fwd;
            Vector3 At(float yaw, float dist) => playerFeet + Quaternion.AngleAxis(yaw, Vector3.up) * back * dist;
            return new[] { At(0, 0.6f), At(45, 0.6f), At(-45, 0.6f), At(0, 0.45f), At(90, 0.45f), At(-90, 0.45f) };
        }

        const float Radius = 0.25f;
        const int IgnoreRaycastLayer = 2; // other display figures live here

        /// <summary>First candidate whose 0.25 m capsule overlaps nothing but the guard's own collider
        /// (walls, furniture and other figures all block). False when every spot is blocked - the caller
        /// retries next frame instead of forcing a spot.</summary>
        public static bool FindClearSpot(Vector3 playerFeet, Vector3 playerForward, Collider ignore, out Vector3 spot)
        {
            int mask = Physics.DefaultRaycastLayers | (1 << IgnoreRaycastLayer);
            foreach (var c in Candidates(playerFeet, playerForward))
            {
                var hits = Physics.OverlapCapsule(c + Vector3.up * (Radius + 0.05f), c + Vector3.up * 1.1f, Radius,
                    mask, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                foreach (var h in hits)
                    if (h != ignore) { blocked = true; break; }
                if (blocked) continue;
                spot = c;
                return true;
            }
            spot = default;
            return false;
        }
    }
}
