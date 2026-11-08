using UnityEditor;
using UnityEngine;

namespace ModelHome.EditorTools
{
    /// <summary>Import rules for Model Home textures (Task 3) and audio (Task 6).</summary>
    public class AssetImportRules : AssetPostprocessor
    {
        const string TextureRoot = "Assets/ModelHome/Textures/";
        const string AudioRoot = "Assets/ModelHome/Audio/";
        static readonly string[] LoopClips = { "hum_roomtone", "fridge_buzz", "clock_tick", "breath_slow" };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureRoot)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            if (assetPath.Contains("_nor_gl_")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.Contains("_mask_") || assetPath.Contains("_rough_") || assetPath.Contains("_ao_"))
                ti.sRGBTexture = false;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot)) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            var s = ai.defaultSampleSettings;
            bool loop = System.Array.Exists(LoopClips, l => assetPath.EndsWith(l + ".wav"));
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            ai.defaultSampleSettings = s;
        }
    }
}
