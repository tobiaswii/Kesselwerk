using UnityEditor;

namespace Kesselwerk.EditorTools
{
    /// <summary>
    /// Sorgt dafür, dass alle Monster-Modelle unter Resources/Monsters mit
    /// "Generic"-Rig und Animationen importiert werden.
    /// </summary>
    public class MonsterImportSettings : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("Kesselwerk/Resources/Monsters")) return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
