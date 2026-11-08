using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ModelHome.EditorTools
{
    /// <summary>Builds the URP Lit material library (plan Task 3). Material names equal the Blender
    /// material names; <see cref="RemapModels"/> points every FBX at these by name.</summary>
    public static class MaterialLibrary
    {
        public const string MaterialDir = "Assets/ModelHome/Materials";
        const string PH = "Assets/ModelHome/Textures/PolyHaven";

        class Spec
        {
            public string Name, Color = "#FFFFFF", PolyHaven, BaseMap, Emission;
            public bool UsePhColour;     // use the Poly Haven diffuse as base map (else flat colour + surface detail)
            public float Smoothness = 0.5f, Metallic, Alpha = 1f;
            public bool Transparent, DoubleSided;
        }

        static readonly List<Spec> Specs = new List<Spec>
        {
            // surfaces with Poly Haven colour
            new Spec { Name = "M_Floor_Wood", PolyHaven = "laminate_floor_02", UsePhColour = true },
            new Spec { Name = "M_Cabinet_Wood", PolyHaven = "kitchen_wood", UsePhColour = true },
            new Spec { Name = "M_Ground_Dirt", PolyHaven = "dirt", UsePhColour = true },
            new Spec { Name = "M_Cloth_Denim", PolyHaven = "denim_fabric", UsePhColour = true, DoubleSided = true },
            // flat show-home colours over Poly Haven surface detail (normal + roughness/AO)
            new Spec { Name = "M_Wall_Plaster", PolyHaven = "plaster_grey_04", Color = "#E9E4DA" },
            new Spec { Name = "M_Ceiling", PolyHaven = "ceiling_interior", Color = "#EDEAE4" },
            new Spec { Name = "M_Fabric_Sofa", PolyHaven = "fabric_pattern_05", Color = "#9AA3A8" },
            new Spec { Name = "M_Rug", PolyHaven = "dirty_carpet", Color = "#C9C2B5" },
            new Spec { Name = "M_Cloth_Shirt", PolyHaven = "fabric_pattern_07", Color = "#AFC4D9", DoubleSided = true },
            new Spec { Name = "M_Cloth_Knit", PolyHaven = "fabric_pattern_07", Color = "#E8E0D0", DoubleSided = true },
            new Spec { Name = "M_Cloth_Trousers", PolyHaven = "fabric_pattern_07", Color = "#3A3D42", DoubleSided = true },
            new Spec { Name = "M_Cloth_Tee", PolyHaven = "fabric_pattern_07", Color = "#F2F2F0", DoubleSided = true },
            new Spec { Name = "M_Cloth_Blanket", PolyHaven = "fabric_pattern_07", Color = "#F4F1EA", DoubleSided = true },
            // flat materials
            new Spec { Name = "M_Counter_Marble", Color = "#E6E3DE", Smoothness = 0.72f },
            new Spec { Name = "M_Door_Paint", Color = "#F1EFEA", Smoothness = 0.35f },
            new Spec { Name = "M_Skirting_Paint", Color = "#F4F2EE", Smoothness = 0.4f },
            new Spec { Name = "M_Metal", Color = "#8C8F93", Metallic = 1f, Smoothness = 0.6f },
            new Spec { Name = "M_Glass", Color = "#B8C8D0", Transparent = true, Alpha = 0.15f, Smoothness = 0.95f },
            new Spec { Name = "M_Lampshade", Color = "#F3E9D2", Smoothness = 0.2f, DoubleSided = true, Emission = "#FFD9A0" },
            new Spec { Name = "M_Paper", Color = "#F2F0EA", Smoothness = 0.1f },
            new Spec { Name = "M_Photo_Family", BaseMap = "Assets/ModelHome/Textures/Photos/photo_family.png", Smoothness = 0.45f },
            new Spec { Name = "M_Newspaper", Color = "#D9D4C7", Smoothness = 0.1f, DoubleSided = true },
            new Spec { Name = "M_Mug", Color = "#E9E6DF", Smoothness = 0.75f },
            new Spec { Name = "M_Ink", Color = "#141414", Smoothness = 0.2f },
            new Spec { Name = "M_Tape", Color = "#E8C320", Smoothness = 0.5f, DoubleSided = true },
            // the display figures: waxy, slightly-too-perfect skin and glass eyes
            new Spec { Name = "M_Skin_Dad", Color = "#E2C8B6", Smoothness = 0.55f },
            new Spec { Name = "M_Skin_Mom", Color = "#E8D3C4", Smoothness = 0.55f },
            new Spec { Name = "M_Skin_Daughter", Color = "#E8D3C4", Smoothness = 0.55f },
            new Spec { Name = "M_Skin_Baby", Color = "#F0DCD0", Smoothness = 0.55f },
            new Spec { Name = "M_Sclera", Color = "#EEEAE2", Smoothness = 0.6f },
            new Spec { Name = "M_Iris_GreyBlue", BaseMap = "Assets/ModelHome/Textures/Eyes/iris_GreyBlue.png", Smoothness = 0.5f },
            new Spec { Name = "M_Iris_Brown", BaseMap = "Assets/ModelHome/Textures/Eyes/iris_Brown.png", Smoothness = 0.5f },
            new Spec { Name = "M_Iris_BlueGrey", BaseMap = "Assets/ModelHome/Textures/Eyes/iris_BlueGrey.png", Smoothness = 0.5f },
            new Spec { Name = "M_Iris_DarkGrey", BaseMap = "Assets/ModelHome/Textures/Eyes/iris_DarkGrey.png", Smoothness = 0.5f },
            new Spec { Name = "M_Cornea", Color = "#FFFFFF", Transparent = true, Alpha = 0.08f, Smoothness = 0.97f },
            new Spec { Name = "M_Hair_Dad", Color = "#3B2E26", Smoothness = 0.7f },
            new Spec { Name = "M_Hair_Mom", Color = "#8C7355", Smoothness = 0.7f },
            new Spec { Name = "M_Hair_Daughter", Color = "#6B5440", Smoothness = 0.7f },
        };

        public static IEnumerable<string> Names
        {
            get { foreach (var s in Specs) yield return s.Name; }
        }

        [MenuItem("Model Home/2. Build Materials")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(MaterialDir))
                AssetDatabase.CreateFolder("Assets/ModelHome", "Materials");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var s in Specs) Build(s, lit);
            AssetDatabase.SaveAssets();
            RemapModels();
            Debug.Log($"[ModelHome] Built {Specs.Count} materials and remapped models.");
        }

        static void Build(Spec s, Shader lit)
        {
            var path = $"{MaterialDir}/{s.Name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit) { name = s.Name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = lit;
            ColorUtility.TryParseHtmlString(s.Color, out var c);
            c.a = s.Alpha;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", s.Metallic);
            m.SetFloat("_Smoothness", s.Smoothness);

            Texture2D baseMap = s.BaseMap != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(s.BaseMap) : null;
            Texture2D normal = null, mask = null;
            if (s.PolyHaven != null)
            {
                if (s.UsePhColour) baseMap = Tex($"{PH}/{s.PolyHaven}/{s.PolyHaven}_diff_2k.jpg");
                normal = Tex($"{PH}/{s.PolyHaven}/{s.PolyHaven}_nor_gl_2k.jpg");
                mask = Tex($"{PH}/{s.PolyHaven}/{s.PolyHaven}_mask_2k.png");
            }
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            Keyword(m, "_NORMALMAP", normal != null);
            m.SetTexture("_MetallicGlossMap", mask);
            Keyword(m, "_METALLICSPECGLOSSMAP", mask != null);
            if (mask != null) m.SetFloat("_Smoothness", 1f); // smoothness comes from the mask alpha
            m.SetTexture("_OcclusionMap", mask);              // AO is packed in the mask's G channel
            Keyword(m, "_OCCLUSIONMAP", mask != null);

            if (s.Transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
                Keyword(m, "_SURFACE_TYPE_TRANSPARENT", true);
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = -1;
                m.SetOverrideTag("RenderType", "Opaque");
                Keyword(m, "_SURFACE_TYPE_TRANSPARENT", false);
                m.SetShaderPassEnabled("DepthOnly", true);
                m.SetShaderPassEnabled("ShadowCaster", true);
            }
            m.SetFloat("_Cull", s.DoubleSided ? 0f : 2f);
            m.doubleSidedGI = s.DoubleSided;
            var glowColor = Color.black;
            bool emits = s.Emission != null && ColorUtility.TryParseHtmlString(s.Emission, out glowColor);
            m.SetColor("_EmissionColor", emits ? glowColor * 1.6f : Color.black);
            Keyword(m, "_EMISSION", emits);
            // URP's material validation keeps _EMISSION only with an emissive GI flag; realtime GI is off in
            // this project, so RealtimeEmissive means "glows, never baked" (the lamps are switchable).
            m.globalIlluminationFlags = emits ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                                              : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
        }

        static Texture2D Tex(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogWarning($"[ModelHome] missing texture {path} (run Tools/textures/fetch_polyhaven.py)");
            return t;
        }

        static void Keyword(Material m, string kw, bool on)
        {
            if (on) m.EnableKeyword(kw);
            else m.DisableKeyword(kw);
        }

        /// <summary>Points every Model Home FBX's embedded materials at the library materials of the same name.</summary>
        public static void RemapModels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/ModelHome/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                mi.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
                mi.SaveAndReimport();
            }
        }
    }
}
