using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace ModelHome.Tests
{
    public class WindowsBuildSettingsTests
    {
        [Test] public void GraphicsApis_D3D12ThenD3D11()
        {
            Assert.IsFalse(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64));
            CollectionAssert.AreEqual(new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 },
                PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64));
        }

        [Test] public void Lightmapper_IsGpu()
        {
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>("Assets/ModelHome/Settings/MH_Lighting.lighting");
            Assert.IsNotNull(ls);
            Assert.AreEqual(LightingSettings.Lightmapper.ProgressiveGPU, ls.lightmapper);
        }

        [Test] public void Standalone_UsesMono() =>
            Assert.AreEqual(ScriptingImplementation.Mono2x, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone));

        [Test] public void Fullscreen_1080pDefault()
        {
            Assert.AreEqual(FullScreenMode.FullScreenWindow, PlayerSettings.fullScreenMode);
            Assert.AreEqual(1920, PlayerSettings.defaultScreenWidth);
            Assert.AreEqual(1080, PlayerSettings.defaultScreenHeight);
        }

        [Test] public void VSync_OnInEveryQualityLevel()
        {
            int current = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    Assert.AreEqual(1, QualitySettings.vSyncCount, QualitySettings.names[i]);
                }
            }
            finally { QualitySettings.SetQualityLevel(current, false); }
        }

        [Test] public void ProductIdentity()
        {
            Assert.AreEqual("Model Home", PlayerSettings.productName);
            Assert.AreEqual("FohYooo", PlayerSettings.companyName);
        }
    }
}
