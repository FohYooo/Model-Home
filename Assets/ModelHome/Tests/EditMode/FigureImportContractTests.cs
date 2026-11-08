using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelHome.Tests
{
    public class FigureImportContractTests
    {
        public static readonly string[] Variants =
            { "Dad_Seated", "Dad_Standing", "Mom_Standing", "Daughter_Seated", "Daughter_Standing", "Baby_Lying" };

        public static GameObject LoadFigure(string v) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ModelHome/Models/Figures/Figure_{v}.fbx");

        static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        [Test] public void Hierarchy_MatchesContract([ValueSource(nameof(Variants))] string v)
        {
            var go = LoadFigure(v);
            Assert.IsNotNull(go, v);
            var head = Find(go.transform, "Head");
            Assert.IsNotNull(head, "Head");
            Assert.IsNotNull(Find(go.transform, "Body"), "Body");
            Assert.IsNotNull(Find(go.transform, "Clothes"), "Clothes");
            foreach (var n in new[] { "Eye_L", "Eye_R", "Lid_L", "Lid_R" })
                Assert.AreEqual(head, Find(go.transform, n)?.parent, $"{n} under Head");
            if (!v.StartsWith("Baby")) Assert.AreEqual(head, Find(go.transform, "Hair")?.parent, "Hair under Head");
        }

        [Test] public void BlendShapes_MatchContract([ValueSource(nameof(Variants))] string v)
        {
            var go = LoadFigure(v);
            void Has(string obj, params string[] shapes)
            {
                var smr = Find(go.transform, obj)?.GetComponent<SkinnedMeshRenderer>();
                Assert.IsNotNull(smr, $"{obj} has no SkinnedMeshRenderer");
                foreach (var s in shapes)
                    Assert.GreaterOrEqual(smr.sharedMesh.GetBlendShapeIndex(s), 0, $"{obj}.{s}");
            }
            Has("Body", "Breathe", "NeckLong");
            Has("Clothes", "Breathe", "NeckLong");
            Has("Head", "SmileWide");
            Has("Lid_L", "Blink");
            Has("Lid_R", "Blink");
        }

        [Test] public void UprightFigures_FaceAlongFacingConvention()
        {
            var go = LoadFigure("Dad_Standing");
            var head = Find(go.transform, "Head");
            var mid = (Find(go.transform, "Eye_L").position + Find(go.transform, "Eye_R").position) / 2f;
            var face = mid - head.position;
            face.y = 0f;
            Assert.Greater(Vector3.Dot(face.normalized, Facing.Of(go.transform)), 0.9f,
                $"face {face} vs Facing.Of(root) {Facing.Of(go.transform)}");
            Assert.Greater(head.position.y, 1.5f, "figure should stand up along +Y");
        }

        [Test] public void Materials_RemappedToLibrary()
        {
            foreach (var v in Variants)
                foreach (var r in LoadFigure(v).GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        Assert.AreEqual($"Assets/ModelHome/Materials/{m.name}.mat", AssetDatabase.GetAssetPath(m), $"{v}/{r.name}");
        }
    }
}
