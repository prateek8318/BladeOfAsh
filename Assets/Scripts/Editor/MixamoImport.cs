using UnityEditor;
using UnityEngine;

// Any FBX dropped into Assets/Models/<Hero>/ is imported as a Humanoid.
//  Character.fbx  = the skinned model (Mixamo: "FBX for Unity", "With Skin", T-pose)
//  <State>.fbx    = animation clips (Mixamo: "Without Skin", "In Place" ticked)
public class MixamoImport : AssetPostprocessor
{
    static readonly string[] Loops = { "Idle", "Run", "Sprint", "Block" };

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Models/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        bool isCharacter = System.IO.Path.GetFileNameWithoutExtension(assetPath) == "Character";
        mi.importAnimation = !isCharacter;
        mi.materialImportMode = isCharacter ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith("Assets/Models/")) return;
        var mi = (ModelImporter)assetImporter;
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        bool loop = System.Array.IndexOf(Loops, name) >= 0;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = loop;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;
            c.lockRootHeightY = true; c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
        }
        mi.clipAnimations = clips;
    }
}
