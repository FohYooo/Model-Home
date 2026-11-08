using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ModelHome.Tests
{
    public class DisplayFigureTests
    {
        GameObject go;

        [TearDown] public void TearDown() { if (go) Object.DestroyImmediate(go); }

        DisplayFigure Spawn(string variant)
        {
            go = Object.Instantiate(FigureImportContractTests.LoadFigure(variant));
            var f = go.AddComponent<DisplayFigure>();
            f.Init();
            return f;
        }

        static SkinnedMeshRenderer Smr(GameObject root, string n) =>
            root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == n);

        [Test] public void Proportions_DriveBlendShapesAndHead()
        {
            var f = Spawn("Dad_Standing");
            var headY = f.Head.position.y;
            f.NeckLong01 = 0.35f;
            f.SmileWide01 = 0.4f;
            f.ApplyPose();
            var body = Smr(go, "Body");
            Assert.AreEqual(35f, body.GetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("NeckLong")), 0.01f);
            var head = Smr(go, "Head");
            Assert.AreEqual(40f, head.GetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("SmileWide")), 0.01f);
            Assert.AreEqual(headY + 0.08f * 0.35f, f.Head.position.y, 0.002f, "head rides up with the neck");
        }

        [Test] public void HeadTilt_RollsAroundTheFaceAxis()
        {
            var f = Spawn("Daughter_Seated");
            var eyeL = f.transform.GetComponentsInChildren<Transform>().First(t => t.name == "Eye_L");
            var before = eyeL.position.y;
            f.HeadTiltDeg = 8f;
            f.ApplyPose();
            Assert.AreNotEqual(before, eyeL.position.y, "one eye rises/falls when the head rolls");
            Assert.AreEqual(0f, Vector3.Angle(f.FaceForward, f.RestFaceForward), 0.5f, "a roll keeps the face looking the same way");
        }

        [Test] public void PlaceFacing_TurnsFaceTowardTarget()
        {
            var f = Spawn("Daughter_Standing");
            f.PlaceFacing(new Vector3(2, 0, 1), new Vector3(2, 1.6f, -3));
            var face = f.FaceForward; face.y = 0;
            Assert.Greater(Vector3.Dot(face.normalized, Vector3.back), 0.98f, $"face {face}");
            Assert.AreEqual(new Vector3(2, 0, 1), f.transform.position);
            Assert.Greater(f.Head.position.y, 1.0f, "a 180 deg turn must stay upright (rotate about world Y only)");
        }

        [Test] public void PlaceFacing_KeepsUpright_ForEveryTurnAngle()
        {
            var f = Spawn("Daughter_Standing");
            for (int deg = 0; deg < 360; deg += 30)
            {
                var dir = Quaternion.AngleAxis(deg, Vector3.up) * Vector3.forward;
                f.PlaceFacing(Vector3.zero, dir * 3f + Vector3.up * 1.5f);
                Assert.Greater(f.Head.position.y, 1.0f, $"turn {deg}");
            }
        }

        [Test] public void MissingChildren_DisableFeature_WithoutThrowing()
        {
            go = new GameObject("Figure_Broken");
            var f = go.AddComponent<DisplayFigure>();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Figure_Broken.*Head"));
            Assert.DoesNotThrow(() => { f.Init(); f.NeckLong01 = 1; f.ApplyPose(); });
        }
    }
}
