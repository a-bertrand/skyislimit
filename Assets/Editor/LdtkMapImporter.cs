using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

/// <summary>Enregistrer le .ldtk dans Assets suffit : Unity le réimporte automatiquement.</summary>
[ScriptedImporter(1, "ldtk")]
public sealed class LdtkMapImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext context)
    {
        var map = ScriptableObject.CreateInstance<LdtkMap>();
        map.json = File.ReadAllText(context.assetPath);
        try { map.Read(); }
        catch (System.Exception exception) { context.LogImportError(exception.Message); }
        context.AddObjectToAsset("map", map);
        context.SetMainObject(map);
    }
}
