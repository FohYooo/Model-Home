using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelHome.Tests
{
    public class MaterialLibraryTests
    {
        public static readonly string[] Names =
        {
            "M_Floor_Wood", "M_Wall_Plaster", "M_Ceiling", "M_Fabric_Sofa", "M_Rug", "M_Cabinet_Wood",
            "M_Counter_Marble", "M_Ground_Dirt", "M_Door_Paint", "M_Skirting_Paint", "M_Metal", "M_Glass",
            "M_Lampshade", "M_Paper", "M_Photo_Family", "M_Skin_Dad", "M_Skin_Mom", "M_Skin_Daughter",
            "M_Skin_Baby", "M_Sclera", "M_Iris_GreyBlue", "M_Iris_Brown", "M_Iris_BlueGrey", "M_Iris_DarkGrey",
            "M_Cornea", "M_Hair_Dad", "M_Hair_Mom", "M_Hair_Daughter", "M_Cloth_Shirt", "M_Cloth_Trousers",
            "M_Cloth_Knit", "M_Cloth_Denim", "M_Cloth_Tee", "M_Cloth_Blanket", "M_Newspaper", "M_Mug",
            "M_Ink", "M_Tape",
        };

        static Material Load(string n) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/ModelHome/Materials/{n}.mat");

        [Test] public void AllContractMaterialsExist_AsUrpLit()
        {
            foreach (var n in Names)
            {
                var m = Load(n);
                Assert.IsNotNull(m, $"{n} missing");
                Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name, n);
            }
        }

        [Test] public void NormalMapsImportedAsNormal()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D _nor_gl_", new[] { "Assets/ModelHome/Textures" }))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                Assert.AreEqual(TextureImporterType.NormalMap, ti.textureType, ti.assetPath);
            }
        }

        [Test] public void MaskMapsAreLinear()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D _mask_", new[] { "Assets/ModelHome/Textures" }))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsFalse(ti.sRGBTexture, ti.assetPath);
            }
        }

        [Test] public void CorneaAndGlassAreTransparent()
        {
            Assert.AreEqual(1f, Load("M_Cornea").GetFloat("_Surface"));
            Assert.AreEqual(1f, Load("M_Glass").GetFloat("_Surface"));
        }

        [Test] public void SkinHasWaxySheen()
        {
            foreach (var n in new[] { "M_Skin_Dad", "M_Skin_Mom", "M_Skin_Daughter", "M_Skin_Baby" })
                Assert.AreEqual(0.55f, Load(n).GetFloat("_Smoothness"), 1e-4, n);
        }

        [Test] public void LampshadeGlows()
        {
            var m = Load("M_Lampshade");
            Assert.IsTrue(m.IsKeywordEnabled("_EMISSION"));
            Assert.Greater(m.GetColor("_EmissionColor").maxColorComponent, 0.5f);
        }

        [Test] public void ClothIsDoubleSided()
        {
            foreach (var n in new[] { "M_Cloth_Shirt", "M_Cloth_Trousers", "M_Cloth_Knit", "M_Cloth_Denim", "M_Cloth_Tee" })
                Assert.AreEqual(0f, Load(n).GetFloat("_Cull"), n);
        }
    }
}
