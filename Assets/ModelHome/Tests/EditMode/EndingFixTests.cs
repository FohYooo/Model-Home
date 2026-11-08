using NUnit.Framework;
using UnityEngine;

namespace ModelHome.Tests
{
    /// <summary>Final-review fixes I1 (the Daughter's face must really be in view and lit before the cut)
    /// and I2 (spawn spots must ignore the guard's own collider but avoid walls and other figures).</summary>
    public class EndingFixTests
    {
        GameObject[] spawned = new GameObject[0];

        [TearDown] public void TearDown()
        {
            foreach (var g in spawned) if (g) Object.DestroyImmediate(g);
        }

        T Make<T>(string name, Vector3 pos) where T : Component
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            System.Array.Resize(ref spawned, spawned.Length + 1);
            spawned[spawned.Length - 1] = go;
            return go.AddComponent<T>();
        }

        Camera Cam()
        {
            var cam = Make<Camera>("TestCam", new Vector3(0, 1.62f, 0));
            cam.fieldOfView = 60f;
            cam.aspect = 16f / 9f;
            return cam;
        }

        // ---------------- I1: sight

        [Test] public void EyesInView_CentreOfView_True()
        {
            var cam = Cam();
            Assert.IsTrue(EndingSight.EyesInView(cam, new Vector3(0, 1.62f, 0.6f)));
        }

        [Test] public void EyesInView_LevelGaze_DaughterEyes35DegBelow_False()
        {
            var cam = Cam();
            var eyes = new Vector3(0, 1.62f - Mathf.Tan(35f * Mathf.Deg2Rad) * 0.6f, 0.6f);
            Assert.IsFalse(EndingSight.EyesInView(cam, eyes), "only her crown clips the bottom edge — not 'seen'");
        }

        [Test] public void EyesInView_Behind_False()
        {
            Assert.IsFalse(EndingSight.EyesInView(Cam(), new Vector3(0, 1.62f, -0.6f)));
        }

        [Test] public void EyesInView_AtSideEdge_False()
        {
            var cam = Cam();
            var edge = cam.ViewportToWorldPoint(new Vector3(0.96f, 0.5f, 0.6f));
            Assert.IsFalse(EndingSight.EyesInView(cam, edge));
        }

        [Test] public void LockedTorch_IgnoresToggle()
        {
            var light = Make<Light>("Torch", Vector3.zero);
            var torch = light.gameObject.AddComponent<Flashlight>();
            torch.Configure();
            torch.SetOn(true);
            torch.Locked = true;
            torch.Toggle();
            Assert.IsTrue(torch.IsOn, "during the ending F must not switch the torch off");
        }

        // ---------------- I2: placement

        CharacterController Guard()
        {
            var cc = Make<CharacterController>("Guard", Vector3.zero);
            cc.height = 1.75f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.875f, 0);
            return cc;
        }

        BoxCollider Box(string name, Vector3 centre, Vector3 size)
        {
            var b = Make<BoxCollider>(name, centre);
            b.size = size;
            return b;
        }

        [Test] public void OpenFloor_PicksDirectlyBehind()
        {
            var guard = Guard();
            Physics.SyncTransforms();
            Assert.IsTrue(EndingPlacement.FindClearSpot(Vector3.zero, Vector3.forward, guard, out var spot));
            Assert.Less(Vector3.Distance(spot, new Vector3(0, 0, -0.6f)), 1e-3f);
        }

        [Test] public void WallBehind_PicksA045Spot_IgnoringTheGuard()
        {
            var guard = Guard();
            Box("Wall", new Vector3(0, 1.3f, -0.5f), new Vector3(4f, 2.6f, 0.1f));
            Physics.SyncTransforms();
            Assert.IsTrue(EndingPlacement.FindClearSpot(Vector3.zero, Vector3.forward, guard, out var spot),
                "a wall 0.5 m behind blocks every 0.6 m spot; a 0.45 m spot beside the guard must still be usable");
            Assert.AreEqual(0.45f, new Vector2(spot.x, spot.z).magnitude, 1e-3f);
            Assert.Greater(spot.z, -0.2f, "the chosen spot must be clear of the wall");
        }

        [Test] public void Enclosed_ReturnsFalse_InsteadOfForcingASpot()
        {
            var guard = Guard();
            Box("Back", new Vector3(0, 1.3f, -0.5f), new Vector3(4f, 2.6f, 0.1f));
            Box("Left", new Vector3(-0.5f, 1.3f, 0f), new Vector3(0.1f, 2.6f, 4f));
            Box("Right", new Vector3(0.5f, 1.3f, 0f), new Vector3(0.1f, 2.6f, 4f));
            Physics.SyncTransforms();
            Assert.IsFalse(EndingPlacement.FindClearSpot(Vector3.zero, Vector3.forward, guard, out _));
        }

        [Test] public void AnotherFigureBehind_IsAvoided()
        {
            var guard = Guard();
            var mom = Make<CapsuleCollider>("Figure_Mom", new Vector3(0, 0.85f, -0.6f));
            mom.radius = 0.25f;
            mom.height = 1.7f;
            mom.gameObject.layer = 2; // figures live on Ignore Raycast
            Physics.SyncTransforms();
            Assert.IsTrue(EndingPlacement.FindClearSpot(Vector3.zero, Vector3.forward, guard, out var spot));
            Assert.Greater(Vector3.Distance(spot, new Vector3(0, 0, -0.6f)), 0.3f, "must not overlap another figure");
        }
    }
}
