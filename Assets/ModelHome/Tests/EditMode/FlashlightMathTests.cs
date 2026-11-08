using NUnit.Framework;
using UnityEngine;

namespace ModelHome.Tests
{
    public class FlashlightMathTests
    {
        [Test] public void PointStraightAhead_InCone() => Assert.IsTrue(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, new Vector3(0, 0, 5)));
        [Test] public void PointBehind_NotInCone() => Assert.IsFalse(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, new Vector3(0, 0, -5)));
        [Test] public void PointBeyondRange_NotInCone() => Assert.IsFalse(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, new Vector3(0, 0, 19)));
        [Test] public void PointAtEdge_InsideHalfAngle() => Assert.IsTrue(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, Quaternion.Euler(0, 27f, 0) * Vector3.forward * 5));
        [Test] public void PointAtOrigin_InCone() => Assert.IsTrue(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, Vector3.zero));
    }
}
