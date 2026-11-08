using System.Collections.Generic;
using NUnit.Framework;

namespace ModelHome.Tests
{
    public class LapModelTests
    {
        [Test] public void Lap_StartsAtLap1() => Assert.AreEqual(LapPhase.Lap1, new LapModel().Phase);

        [Test] public void Lap_IncompleteChecklist_DoesNotAdvance()
        {
            var l = new LapModel();
            Assert.IsFalse(l.TryAdvance(false));
            Assert.AreEqual(LapPhase.Lap1, l.Phase);
        }

        [Test] public void Lap_AdvancesInOrder()
        {
            var l = new LapModel();
            var seen = new List<LapPhase>();
            l.PhaseChanged += seen.Add;
            l.TryAdvance(true); l.TryAdvance(true); l.TryAdvance(true);
            CollectionAssert.AreEqual(new[] { LapPhase.Lap2, LapPhase.Lap3, LapPhase.Ending }, seen);
        }

        [Test] public void Lap_EndingIsTerminal()
        {
            var l = new LapModel();
            for (int i = 0; i < 3; i++) l.TryAdvance(true);
            Assert.IsFalse(l.TryAdvance(true));
            Assert.AreEqual(LapPhase.Ending, l.Phase);
        }
    }
}
