using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelHome.Tests
{
    public class LapStagingAssetsTests
    {
        static LapStaging Lap(int n) => AssetDatabase.LoadAssetAtPath<LapStaging>($"Assets/ModelHome/Staging/Lap{n}.asset");
        static FigureStaging Fig(LapStaging s, FigureId id) => s.figures.FirstOrDefault(f => f.figure == id);

        [Test] public void ClockTexts()
        {
            Assert.AreEqual("01:00", Lap(1).clockText);
            Assert.AreEqual("02:00", Lap(2).clockText);
            Assert.AreEqual("03:00", Lap(3).clockText);
        }

        [Test] public void TaskSequences_MatchSpec()
        {
            CollectionAssert.AreEqual(new[] { "lock_front", "latch_study_window", "lamp_living_off", "lock_back" }, Lap(1).tasks.Select(t => t.id));
            CollectionAssert.AreEqual(new[] { "latch_kitchen_window", "lamp_study_off", "latch_utility_window", "lock_back" }, Lap(2).tasks.Select(t => t.id));
            CollectionAssert.AreEqual(new[] { "latch_kitchen_window", "count_figures", "lamp_study_off", "lock_back" }, Lap(3).tasks.Select(t => t.id));
            Assert.AreEqual("Confirm display figures present (4)", Lap(3).tasks[1].label);
        }

        [Test] public void Lap1_AllStill()
        {
            foreach (var f in Lap(1).figures)
            {
                Assert.IsFalse(f.eyeTracking || f.breathing || f.blinkOnGlanceAway, f.variant);
                Assert.AreEqual(0f, f.headTiltDeg, f.variant);
            }
            Assert.AreEqual(4, Lap(1).figures.Length);
        }

        [Test] public void Lap2_Staging()
        {
            var s = Lap(2);
            Assert.AreEqual(8f, Fig(s, FigureId.Daughter).headTiltDeg);
            Assert.AreEqual("Anchor_Mom_FacingHall", Fig(s, FigureId.Mom).anchor);
            Assert.IsTrue(Fig(s, FigureId.Dad).breathing);
            Assert.IsTrue(s.figures.All(f => f.eyeTracking && f.blinkOnGlanceAway));
            Assert.IsTrue(s.inhaleCueNearDaughter);
        }

        [Test] public void Lap3_Staging()
        {
            var s = Lap(3);
            var dad = Fig(s, FigureId.Dad);
            Assert.AreEqual("Dad_Standing", dad.variant);
            Assert.AreEqual(0.35f, dad.neckLong01, 1e-5);
            Assert.AreEqual("Anchor_Dad_LivingDoorway", dad.anchor);
            var mom = Fig(s, FigureId.Mom);
            Assert.AreEqual(0.40f, mom.smileWide01, 1e-5);
            Assert.AreEqual("Anchor_Mom_BackHallDoor", mom.anchor);
            Assert.IsNull(Fig(s, FigureId.Daughter), "Daughter must be absent (empty chair)");
        }

        [Test] public void AnchorsAndVariants_Exist()
        {
            var house = AssetDatabase.LoadAssetAtPath<GameObject>(HouseImportContractTests.HousePath);
            var names = house.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToHashSet();
            for (int n = 1; n <= 3; n++)
                foreach (var f in Lap(n).figures)
                {
                    Assert.IsTrue(names.Contains(f.anchor), $"Lap{n} {f.variant} anchor {f.anchor}");
                    CollectionAssert.Contains(FigureImportContractTests.Variants, f.variant);
                }
        }
    }
}
