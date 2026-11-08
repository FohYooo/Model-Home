using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ModelHome.Tests
{
    public class ProjectSetupTests
    {
        [Test]
        public void DefaultPipeline_IsMhUrpAsset()
        {
            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(rp, "default render pipeline is not a URP asset");
            Assert.AreEqual("MH_URP", rp.name);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                Assert.AreSame(rp, QualitySettings.GetRenderPipelineAssetAt(i), $"quality level {QualitySettings.names[i]}");
        }

        [Test]
        public void Renderer_HasSsaoFeature_AndForwardPlus()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/ModelHome/Settings/MH_Renderer.asset");
            Assert.IsNotNull(data, "MH_Renderer.asset missing");
            Assert.IsTrue(data.rendererFeatures.Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"));
            Assert.AreEqual(RenderingMode.ForwardPlus, data.renderingMode);
        }

        [Test]
        public void PostProfile_HasSpecEffects()
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/ModelHome/Settings/MH_PostProfile.asset");
            Assert.IsNotNull(p, "MH_PostProfile.asset missing");
            Assert.IsTrue(p.TryGet<Tonemapping>(out var t));
            Assert.AreEqual(TonemappingMode.ACES, t.mode.value);
            Assert.IsTrue(p.Has<FilmGrain>());
            Assert.IsTrue(p.Has<Vignette>());
            Assert.IsTrue(p.Has<ChromaticAberration>());
            Assert.IsTrue(p.TryGet<ColorAdjustments>(out var c));
            Assert.Less(c.postExposure.value, 0f);
        }

        [Test]
        public void ColorSpace_IsLinear()
        {
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
        }
    }
}
