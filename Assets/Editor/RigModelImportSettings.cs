using UnityEditor;

namespace OCS.VR.EditorTools
{
    /// <summary>
    /// Import settings for the models from tools/blender/generate_rig_parts.py, applied
    /// automatically to anything in Assets/Art/Models.
    ///
    /// Lightmap UVs matter most: the design calls for baked lighting on the rig, and
    /// baking needs a second, non overlapping UV set that the generator doesn't make.
    /// </summary>
    public class RigModelImportSettings : AssetPostprocessor
    {
        const string ModelsFolder = "Assets/Art/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder)) return;

            var importer = (ModelImporter)assetImporter;
            importer.generateSecondaryUV = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = false;      // keeps a CPU copy of the mesh out of memory
            // Each model has one root. Unity drops a lone root by default, which puts
            // Fan0, Fan1 and LED directly under the prefab, where CardVisual looks for them.
            // Pinned here so a changed default can't break the lookup.
            importer.preserveHierarchy = false;
        }
    }
}
