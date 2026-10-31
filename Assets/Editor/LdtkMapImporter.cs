using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(2, "ldtk")]
public sealed class LdtkMapImporter : ScriptedImporter
{
    public const string ToolboxPath = "Assets/_Game/Data/WorldToolbox.asset";
    public WorldToolbox toolbox;
    public override void OnImportAsset(AssetImportContext context)
    {
        var map = ScriptableObject.CreateInstance<LdtkMap>();
        map.json = File.ReadAllText(context.assetPath);
        map.toolbox = toolbox != null ? toolbox : AssetDatabase.LoadAssetAtPath<WorldToolbox>(ToolboxPath);
        if (map.toolbox != null) context.DependsOnSourceAsset(AssetDatabase.GetAssetPath(map.toolbox));
        try
        {
            var project = map.Read();
            var defs = ToolboxJson.Object(map.json)["defs"].Obj();
            var textures = new List<LdtkTexture>();
            foreach (var item in defs["tilesets"].List())
            {
                var def = item.Obj();
                if (!(def["relPath"] is string relative)) continue;
                string full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(context.assetPath), relative));
                string path = FileUtil.GetProjectRelativePath(full);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) throw new FormatException("Tileset hors Assets : " + relative);
                context.DependsOnSourceAsset(path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) throw new FormatException("PNG introuvable : " + path);
                textures.Add(new LdtkTexture { uid = Convert.ToInt32(def["uid"]), texture = texture });
            }
            map.textures = textures.ToArray();
            foreach (var level in project.levels)
                foreach (var layer in level.layerInstances)
                    foreach (var tile in (layer.gridTiles ?? Array.Empty<LdtkTile>()).Concat(layer.autoLayerTiles ?? Array.Empty<LdtkTile>()))
                    {
                        var texture = map.Texture(layer.__tilesetDefUid);
                        if (texture == null || tile.src[0] + layer.__gridSize > texture.width || tile.src[1] + layer.__gridSize > texture.height)
                            throw new FormatException("Tuile hors du PNG ou tileset absent : " + layer.__identifier);
                    }
        }
        catch (Exception exception) { context.LogImportError(exception.Message); }
        context.AddObjectToAsset("map", map);
        context.SetMainObject(map);
    }
}
