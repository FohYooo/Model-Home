using UnityEditor;

namespace ModelHome.EditorTools
{
    /// <summary>Import rules for the Blender-generated FBX files (Tasks 4 and 5).
    /// No axis bake: roots keep Unity's -90 X import rotation, so gameplay code reads facing through
    /// <see cref="ModelHome.Facing"/>. Animation type stays Generic so blend-shape meshes get
    /// SkinnedMeshRenderers (with None they fall back to MeshRenderer and ignore blend shapes).</summary>
    public class ModelImportRules : AssetPostprocessor
    {
        const string FigureRoot = "Assets/ModelHome/Models/Figures/";
        const string HouseRoot = "Assets/ModelHome/Models/House/";

        void OnPreprocessModel()
        {
            bool figure = assetPath.StartsWith(FigureRoot);
            bool house = assetPath.StartsWith(HouseRoot);
            if (!figure && !house) return;
            var mi = (ModelImporter)assetImporter;
            mi.bakeAxisConversion = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.importBlendShapes = figure;
            mi.addCollider = house;
            mi.generateSecondaryUV = house;
            mi.isReadable = house; // MeshColliders and probes need CPU-side mesh data
        }
    }
}
