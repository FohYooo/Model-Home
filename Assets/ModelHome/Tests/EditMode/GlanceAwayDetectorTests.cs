using NUnit.Framework;

namespace ModelHome.Tests
{
    public class GlanceAwayDetectorTests
    {
        [Test] public void Glance_FiresWhenGazeLeaves()
        {
            var g = new GlanceAwayDetector(25, 5);
            Assert.IsFalse(g.Update(true, 5, 0));
            Assert.IsTrue(g.Update(true, 30, 0.1f));
        }

        [Test] public void Glance_RespectsCooldown()
        {
            var g = new GlanceAwayDetector(25, 5);
            g.Update(true, 5, 0); g.Update(true, 30, 0.1f); g.Update(true, 5, 1);
            Assert.IsFalse(g.Update(true, 30, 2));
            g.Update(true, 5, 6);
            Assert.IsTrue(g.Update(true, 30, 6.1f));
        }

        [Test] public void Glance_OffScreen_NeverFires()
        {
            var g = new GlanceAwayDetector(25, 5);
            g.Update(true, 5, 0);
            Assert.IsFalse(g.Update(false, 30, 0.1f));
        }
    }
}
