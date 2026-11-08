using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ModelHome.EditorTools
{
    /// <summary>Idempotent project setup (plan Task 1): URP asset + Forward+ renderer with SSAO,
    /// assigned to Graphics and every quality level; the global post-processing profile; linear colour.</summary>
    public static class ProjectSetup
    {
        public const string SettingsDir = "Assets/ModelHome/Settings";
        public const string UrpPath = SettingsDir + "/MH_URP.asset";
        public const string RendererPath = SettingsDir + "/MH_Renderer.asset";
        public const string ProfilePath = SettingsDir + "/MH_PostProfile.asset";

        [MenuItem("Model Home/1. Project Setup")]
        public static void Run()
        {
            EnsureFolder(SettingsDir);
            var renderer = EnsureRenderer();
            var urp = EnsurePipelineAsset(renderer);
            AssignPipeline(urp);
            EnsurePostProfile();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            Debug.Log("[ModelHome] Project setup complete (URP + post profile).");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static UniversalRendererData EnsureRenderer()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
            {
                // URP's own internal factory also assigns the default PostProcessData.
                var create = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererData",
                    BindingFlags.NonPublic | BindingFlags.Static);
                data = create != null
                    ? (UniversalRendererData)create.Invoke(null, new object[] { RendererType.UniversalRenderer })
                    : ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, RendererPath);
            }
            data.renderingMode = RenderingMode.ForwardPlus;

            if (!data.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion))
            {
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                if (map != null)
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(data);
            return data;
        }

        static UniversalRenderPipelineAsset EnsurePipelineAsset(UniversalRendererData renderer)
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, UrpPath);
            }
            urp.supportsHDR = true;
            urp.msaaSampleCount = 1;
            urp.shadowDistance = 25f;
            urp.shadowCascadeCount = 2;
            var so = new SerializedObject(urp);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetInt(so, "m_AdditionalLightsRenderingMode", 1); // per pixel
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 2048);
            SetInt(so, "m_MainLightShadowmapResolution", 2048);
            SetBool(so, "m_SupportsCameraDepthTexture", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            return urp;
        }

        static void AssignPipeline(UniversalRenderPipelineAsset urp)
        {
            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void EnsurePostProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var grain = Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.8f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);

            Get<ChromaticAberration>(profile).intensity.Override(0.08f);

            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(-0.4f);
            color.contrast.Override(12f);
            color.saturation.Override(-25f);
            ColorUtility.TryParseHtmlString("#D8E4F0", out var filter);
            color.colorFilter.Override(filter);

            Get<WhiteBalance>(profile).temperature.Override(-12f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.2f);

            EditorUtility.SetDirty(profile);
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing)) return existing;
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        static void SetBool(SerializedObject so, string prop, bool v)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = v;
            else Debug.LogWarning($"[ModelHome] URP asset has no property {prop}");
        }

        static void SetInt(SerializedObject so, string prop, int v)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.intValue = v;
            else Debug.LogWarning($"[ModelHome] URP asset has no property {prop}");
        }
    }
}
