using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelHome.Tests
{
    public class AudioImportTests
    {
        static readonly string[] Loops = { "hum_roomtone", "fridge_buzz", "clock_tick", "breath_slow" };

        [Test] public void AllClipsMono_LoopsCompressedInMemory_OthersDecompressOnLoad()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/ModelHome/Audio" });
            Assert.AreEqual(18, guids.Length);
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var ai = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.IsTrue(ai.forceToMono, path);
                bool loop = System.Array.Exists(Loops, l => path.EndsWith(l + ".wav"));
                Assert.AreEqual(loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad,
                    ai.defaultSampleSettings.loadType, path);
            }
        }
    }
}
