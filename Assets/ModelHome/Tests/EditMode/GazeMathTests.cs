using NUnit.Framework;
using UnityEngine;

namespace ModelHome.Tests
{
    public class GazeMathTests
    {
        [Test] public void Clamp_WithinCone_Unchanged()
        {
            var d = Quaternion.Euler(0, 20, 0) * Vector3.forward;
            Assert.That(Vector3.Angle(d, GazeMath.ClampToCone(Vector3.forward, d, 30)), Is.LessThan(0.01f));
        }

        [Test] public void Clamp_Behind_LimitedTo30()
        {
            var r = GazeMath.ClampToCone(Vector3.forward, Vector3.back + Vector3.right * 0.01f, 30);
            Assert.That(Vector3.Angle(Vector3.forward, r), Is.EqualTo(30).Within(0.1f));
        }

        [Test] public void Clamp_StraightUp_LimitedTo30() =>
            Assert.That(Vector3.Angle(Vector3.forward, GazeMath.ClampToCone(Vector3.forward, Vector3.up, 30)), Is.EqualTo(30).Within(0.1f));

        [Test] public void Clamp_ExactlyBehind_ReturnsValidDirection()
        {
            var r = GazeMath.ClampToCone(Vector3.forward, Vector3.back, 30);
            Assert.That(r.magnitude, Is.EqualTo(1).Within(1e-4));
            Assert.That(Vector3.Angle(Vector3.forward, r), Is.EqualTo(30).Within(0.1f));
        }
    }
}
