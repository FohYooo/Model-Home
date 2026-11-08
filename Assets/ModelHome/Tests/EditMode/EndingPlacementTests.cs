using NUnit.Framework;
using UnityEngine;

namespace ModelHome.Tests
{
    public class EndingPlacementTests
    {
        [Test] public void FirstCandidate_IsDirectlyBehind_AtPoint6()
        {
            var c = EndingPlacement.Candidates(Vector3.zero, Vector3.forward);
            Assert.That(Vector3.Distance(c[0], new Vector3(0, 0, -0.6f)), Is.LessThan(1e-4));
        }

        [Test] public void Candidates_AllOnPlayerFloor()
        {
            foreach (var p in EndingPlacement.Candidates(new Vector3(1, 2, 3), Vector3.right))
                Assert.That(p.y, Is.EqualTo(2).Within(1e-4));
        }

        [Test] public void Candidates_NoneInFront()
        {
            foreach (var p in EndingPlacement.Candidates(Vector3.zero, Vector3.forward))
                Assert.That(Vector3.Dot(p, Vector3.forward), Is.LessThanOrEqualTo(1e-4));
        }

        [Test] public void Candidates_IgnoreLookPitch()
        {
            var c = EndingPlacement.Candidates(Vector3.zero, new Vector3(0, -0.9f, 0.1f));
            Assert.That(Vector3.Distance(c[0], new Vector3(0, 0, -0.6f)), Is.LessThan(1e-3));
        }

        [Test] public void Candidates_Count6() => Assert.AreEqual(6, EndingPlacement.Candidates(Vector3.zero, Vector3.forward).Count);
    }
}
