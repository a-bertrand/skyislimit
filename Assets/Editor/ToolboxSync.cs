using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Modifie seulement les définitions gérées. Les placements et propriétés LDtk inconnues sont conservés.</summary>
public static class ToolboxSync
{
    private static Dictionary<string, object> Clone(Dictionary<string, object> value) => ToolboxJson.Object(ToolboxJson.Write(value));
    private static int Id(Dictionary<string, object> value) => Convert.ToInt32(value["uid"]);
    private static int Next(Dictionary<string, object> doc) { int n = Convert.ToInt32(doc["nextUid"]); doc["nextUid"] = n + 1; return n; }
    public static IEnumerable<Dictionary<string, object>> Entities(Dictionary<string, object> doc) => doc["levels"].List()
        .SelectMany(l => l.Obj()["layerInstances"].List()).SelectMany(l => l.Obj()["entityInstances"].List()).Select(e => e.Obj());

    public static string Synchronize(string json, string mapPath, WorldToolbox catalog)
    {
        catalog.Validate();
        var doc = ToolboxJson.Object(json);
        var defs = doc["defs"].Obj();
        var entities = defs["entities"].List();
        var template = entities.First(e => (string)e.Obj()["identifier"] == "Drone").Obj();
        var fieldTemplate = template["fieldDefs"].List()[0].Obj();
        // Ne retire que les définitions précédemment créées par cette boîte à outils.
        foreach (var raw in entities.ToArray())
        {
            var def = raw.Obj();
            if (!(def["tags"].List().Contains("UnityToolbox")) || catalog.Find((string)def["identifier"]) != null) continue;
            if (Entities(doc).Any(e => Convert.ToInt32(e["defUid"]) == Id(def)))
                throw new InvalidOperationException(def["identifier"] + " est encore placé sur une carte. Retire ses exemplaires dans LDtk avant de synchroniser sa suppression.");
            entities.Remove(raw);
        }
        foreach (var entry in catalog.entries)
        {
            var def = entities.Select(e => e.Obj()).FirstOrDefault(e => (string)e["identifier"] == entry.id);
            if (def == null)
            {
                def = Clone(template); def["uid"] = Next(doc); def["identifier"] = entry.id;
                def["fieldDefs"] = new List<object>(); entities.Add(def);
            }
            if (!def["tags"].List().Contains("UnityToolbox")) def["tags"].List().Add("UnityToolbox");
            def["doc"] = "Géré par la boîte à outils Unity. Modifier les placements et les champs locaux dans LDtk.";
            var tile = SpriteTile(doc, mapPath, entry.sprite);
            def["tileRect"] = tile; def["uiTileRect"] = tile; def["tilesetId"] = tile["tilesetUid"];
            def["renderMode"] = "Tile"; def["tileRenderMode"] = "FullSizeUncropped";
            def["width"] = Convert.ToInt32(tile["w"]); def["height"] = Convert.ToInt32(tile["h"]);
            def["pivotX"] = entry.sprite.pivot.x / entry.sprite.rect.width;
            def["pivotY"] = 1 - entry.sprite.pivot.y / entry.sprite.rect.height;
            if (entry.behaviour == ToolboxBehaviour.Enemy)
            {
                Field(doc, def, fieldTemplate, "PatrolRadius", "Float", entry.patrolRadius);
                Field(doc, def, fieldTemplate, "Speed", "Float", entry.speed);
            }
            if (entry.behaviour == ToolboxBehaviour.Sign) Field(doc, def, fieldTemplate, "Text", "String", entry.text);
            if (entry.behaviour == ToolboxBehaviour.Portal) Field(doc, def, fieldTemplate, "Destination", "String", "");
            foreach (var instance in Entities(doc).Where(e => Convert.ToInt32(e["defUid"]) == Id(def)))
            {
                instance["__tile"] = tile;
                instance["__pivot"] = new object[] { def["pivotX"], def["pivotY"] };
            }
        }
        // LDtk relit realEditorValues, tandis que le jeu lit __value. Garder les deux représentations.
        foreach (var instance in Entities(doc))
            foreach (var raw in instance["fieldInstances"].List())
            {
                var field = raw.Obj();
                if (!field.ContainsKey("__tile")) field["__tile"] = null;
                if (field.TryGetValue("__value", out var value) && value != null &&
                    (!field.TryGetValue("realEditorValues", out var values) || values == null || values.List().Count == 0))
                    field["realEditorValues"] = new object[] { new Dictionary<string, object> {
                        { "id", value is string ? "V_String" : "V_Float" }, { "params", new object[] { value } }
                    } };
            }
        return ToolboxJson.Write(doc);
    }

    private static void Field(Dictionary<string, object> doc, Dictionary<string, object> entity, Dictionary<string, object> template, string name, string type, object value)
    {
        var fields = entity["fieldDefs"].List();
        var field = fields.Select(f => f.Obj()).FirstOrDefault(f => (string)f["identifier"] == name);
        if (field == null) { field = Clone(template); field["uid"] = Next(doc); fields.Add(field); }
        field["identifier"] = name; field["__type"] = type; field["type"] = "F_" + type;
        field["min"] = type == "Float" ? (object)0 : null;
        field["max"] = name == "PatrolRadius" ? (object)128 : name == "Speed" ? (object)4 : null;
        field["defaultOverride"] = new Dictionary<string, object> { { "id", "V_" + type }, { "params", new object[] { value } } };
        foreach (var instance in Entities(doc).Where(e => Convert.ToInt32(e["defUid"]) == Id(entity)))
        {
            var values = instance["fieldInstances"].List();
            if (!values.Any(f => (string)f.Obj()["__identifier"] == name))
                values.Add(new Dictionary<string, object> { { "__identifier", name }, { "__type", type }, { "__value", value }, { "defUid", field["uid"] }, { "realEditorValues", new object[0] } });
        }
    }

    private static Dictionary<string, object> SpriteTile(Dictionary<string, object> doc, string mapPath, Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        int uid = AddTileset(doc, mapPath, path, 16);
        var rect = sprite.rect;
        return new Dictionary<string, object> { { "tilesetUid", uid }, { "x", (int)rect.x }, { "y", sprite.texture.height - (int)rect.yMax }, { "w", (int)rect.width }, { "h", (int)rect.height } };
    }

    public static int AddTileset(Dictionary<string, object> doc, string mapPath, string texturePath, int size)
    {
        if (!texturePath.StartsWith("Assets/", StringComparison.Ordinal) || !texturePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choisir un PNG importé dans Assets.");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null) throw new InvalidOperationException("PNG introuvable.");
        string relative = new Uri(Path.GetFullPath(mapPath)).MakeRelativeUri(new Uri(Path.GetFullPath(texturePath))).ToString();
        relative = Uri.UnescapeDataString(relative);
        var sets = doc["defs"].Obj()["tilesets"].List();
        var existing = sets.Select(t => t.Obj()).FirstOrDefault(t => (string)t["relPath"] == relative && Convert.ToInt32(t["tileGridSize"]) == size);
        if (existing != null) return Id(existing);
        var def = Clone(sets[0].Obj()); int uid = Next(doc);
        def["uid"] = uid; def["identifier"] = "ToolboxTiles_" + uid; def["relPath"] = relative;
        def["pxWid"] = texture.width; def["pxHei"] = texture.height; def["tileGridSize"] = size;
        def["__cWid"] = texture.width / size; def["__cHei"] = texture.height / size;
        def["spacing"] = 0; def["padding"] = 0; def["cachedPixelData"] = null;
        def["enumTags"] = new object[0]; def["customData"] = new object[0]; def["savedSelections"] = new object[0];
        sets.Add(def); return uid;
    }

    public static string AddTileLayer(string json, string path, Texture2D texture, int size)
    {
        if (size < 8 || texture.width % size != 0 || texture.height % size != 0) throw new InvalidOperationException("La taille du PNG doit être un multiple de la taille des cases (8 px minimum).");
        var doc = ToolboxJson.Object(json); int tileset = AddTileset(doc, path, AssetDatabase.GetAssetPath(texture), size);
        var layers = doc["defs"].Obj()["layers"].List();
        var layer = Clone(layers.First(l => (string)l.Obj()["type"] == "IntGrid").Obj());
        int uid = Next(doc); string name = "Decor_" + uid;
        layer["uid"] = uid; layer["identifier"] = name; layer["type"] = "Tiles"; layer["__type"] = "Tiles";
        layer["gridSize"] = size; layer["tilesetDefUid"] = tileset; layer["autoTilesetDefUid"] = null;
        layer["intGridValues"] = new object[0]; layer["autoRuleGroups"] = new object[0];
        layers.Insert(0, layer);
        foreach (var level in doc["levels"].List().Select(l => l.Obj()))
        {
            var instances = level["layerInstances"].List();
            var instance = Clone(instances.First(l => (string)l.Obj()["__type"] == "IntGrid").Obj());
            instance["iid"] = Guid.NewGuid().ToString(); instance["layerDefUid"] = uid;
            instance["__identifier"] = name; instance["__type"] = "Tiles"; instance["__gridSize"] = size;
            instance["__cWid"] = (int)Math.Ceiling(Convert.ToDouble(level["pxWid"]) / size);
            instance["__cHei"] = (int)Math.Ceiling(Convert.ToDouble(level["pxHei"]) / size);
            instance["__tilesetDefUid"] = tileset;
            instance["__tilesetRelPath"] = doc["defs"].Obj()["tilesets"].List().Select(t => t.Obj()).First(t => Id(t) == tileset)["relPath"];
            instance["intGridCsv"] = new object[0]; instance["gridTiles"] = new object[0]; instance["autoLayerTiles"] = new object[0]; instance["entityInstances"] = new object[0];
            instances.Insert(0, instance);
        }
        return ToolboxJson.Write(doc);
    }

    public static string AddLevel(string json, string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name ?? "", "^[A-Za-z][A-Za-z0-9_]*$")) throw new InvalidOperationException("Nom de carte : lettres, chiffres et _ ; commencer par une lettre.");
        var doc = ToolboxJson.Object(json); var levels = doc["levels"].List();
        if (levels.Any(l => (string)l.Obj()["identifier"] == name)) throw new InvalidOperationException("Ce niveau existe déjà.");
        var level = Clone(levels[0].Obj()); level["identifier"] = name; level["iid"] = Guid.NewGuid().ToString(); level["uid"] = Next(doc);
        level["worldX"] = levels.Count * (Convert.ToInt32(level["pxWid"]) + 32); level["worldY"] = 0;
        level["__neighbours"] = new object[0];
        foreach (var raw in level["layerInstances"].List())
        {
            var layer = raw.Obj(); layer["iid"] = Guid.NewGuid().ToString(); layer["levelId"] = level["uid"];
            layer["gridTiles"] = new object[0]; layer["autoLayerTiles"] = new object[0];
            var entities = layer["entityInstances"].List();
            entities.RemoveAll(e => (string)e.Obj()["__identifier"] != "PlayerSpawn");
            foreach (var spawn in entities) spawn.Obj()["iid"] = Guid.NewGuid().ToString();
            if ((string)layer["__identifier"] == "Terrain")
            {
                int w = Convert.ToInt32(layer["__cWid"]), h = Convert.ToInt32(layer["__cHei"]);
                layer["intGridCsv"] = Enumerable.Range(0, w * h).Select(i => (object)(i % w == 0 || i % w == w - 1 || i / w == 0 || i / w == h - 1 ? 3 : 2)).ToList();
                var spawn = level["layerInstances"].List().SelectMany(l => l.Obj()["entityInstances"].List()).FirstOrDefault(e => (string)e.Obj()["__identifier"] == "PlayerSpawn");
                if (spawn != null) { spawn.Obj()["px"] = new object[] { 40, 40 }; spawn.Obj()["__grid"] = new object[] { 2, 2 }; }
            }
        }
        levels.Add(level); return ToolboxJson.Write(doc);
    }

    public static void Save(string path, string content)
    {
        // Hors Assets pour ne pas importer les sauvegardes comme des projets jouables.
        Directory.CreateDirectory("ldtk-maps/backups/toolbox");
        string backup = "ldtk-maps/backups/toolbox/" + Path.GetFileNameWithoutExtension(path) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".ldtk";
        File.Copy(path, backup);
        File.WriteAllText(path, content);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
