using NUnit.Framework;

namespace ModelHome.Tests
{
    public class ChecklistModelTests
    {
        static ChecklistModel M() => new ChecklistModel(new[] { new ChecklistTask("a", "A"), new ChecklistTask("b", "B") });

        [Test] public void New_NotComplete()
        {
            var m = M();
            Assert.IsFalse(m.AllComplete);
            Assert.AreEqual(2, m.RemainingCount);
        }

        [Test] public void CompleteAll_AllComplete()
        {
            var m = M();
            Assert.IsTrue(m.Complete("a"));
            Assert.IsTrue(m.Complete("b"));
            Assert.IsTrue(m.AllComplete);
        }

        [Test] public void UnknownId_ReturnsFalse_NoChange()
        {
            var m = M();
            Assert.IsFalse(m.Complete("zzz"));
            Assert.AreEqual(2, m.RemainingCount);
        }

        [Test] public void DoubleComplete_ReturnsFalse_EventOnce()
        {
            var m = M();
            int n = 0;
            m.TaskCompleted += _ => n++;
            m.Complete("a");
            Assert.IsFalse(m.Complete("a"));
            Assert.AreEqual(1, n);
        }

        [Test] public void Load_ResetsState()
        {
            var m = M();
            m.Complete("a");
            m.Load(new[] { new ChecklistTask("a", "A") });
            Assert.IsFalse(m.IsDone("a"));
            Assert.AreEqual(1, m.RemainingCount);
        }
    }
}
