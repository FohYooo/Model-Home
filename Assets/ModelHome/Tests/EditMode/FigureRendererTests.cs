using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelHome.Tests
{
    public class FigureRendererTests
    {
        [Test] public void FigureImporter_KeepsSkinnedRenderers_NoAxisBake()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath("Assets/ModelHome/Models/Figures/Figure_Dad_Standing.fbx");
            Assert.IsFalse(mi.bakeAxisConversion);
            Assert.AreNotEqual(ModelImporterAnimationType.None, mi.animationType);
            Assert.IsTrue(mi.importBlendShapes);
        }
    }
}
