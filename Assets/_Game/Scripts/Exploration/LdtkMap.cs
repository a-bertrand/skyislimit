using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Données LDtk embarquées dans le jeu par LdtkMapImporter. Aucune lecture disque en build.</summary>
public sealed class LdtkMap : ScriptableObject
{
    [HideInInspector] public string json;
    public WorldToolbox toolbox;
    public LdtkTexture[] textures = Array.Empty<LdtkTexture>();
    public LdtkProject Read() => LdtkProject.Parse(json, toolbox);
    public Texture2D Texture(int uid) => textures.FirstOrDefault(t => t.uid == uid)?.texture;
}

[Serializable]
public sealed class LdtkProject
{
    public bool externalLevels;
    public LdtkLevel[] levels;
    public LdtkWorld[] worlds;

    public static LdtkProject Parse(string json, WorldToolbox toolbox = null)
    {
        // Les champs LDtk ont des valeurs hétérogènes. Les normaliser pour JsonUtility.
        var document = ToolboxJson.Object(json);
        if (document.TryGetValue("levels", out var rawLevels))
            foreach (var raw in rawLevels.List())
                if (raw.Obj().TryGetValue("layerInstances", out var layers) && layers != null)
                    foreach (var layer in layers.List())
                        if (layer.Obj().TryGetValue("entityInstances", out var entities))
                            foreach (var entity in entities.List())
                                foreach (var field in entity.Obj()["fieldInstances"].List())
                                {
                                    var f = field.Obj(); if (!f.TryGetValue("__value", out var value)) continue;
                                    f.Remove("__value");
                                    if (value is string text) f["textValue"] = text;
                                    else if (value is double number) f["numberValue"] = number;
                                }
        var project = JsonUtility.FromJson<LdtkProject>(ToolboxJson.Write(document));
        if (project == null || project.externalLevels)
            throw new FormatException("Prototype : désactiver External levels dans LDtk.");
        if (project.worlds != null && project.worlds.Length > 0)
            throw new FormatException("Prototype : utiliser le monde unique LDtk (pas Multi-worlds).");
        if (project.levels == null || project.levels.Length == 0)
            throw new FormatException("Prototype : le projet LDtk doit contenir au moins un niveau.");
        var ids = new HashSet<string>();
        var names = new HashSet<string>();
        foreach (var level in project.levels)
        {
            if (string.IsNullOrEmpty(level.iid) || !ids.Add(level.iid) || !names.Add(level.identifier))
                throw new FormatException("Nom ou IID de niveau absent / dupliqué.");
            level.Validate(toolbox);
        }
        foreach (var level in project.levels)
            foreach (var entity in level.Entities.Where(e => e.__identifier == "Portal" || toolbox?.Find(e.__identifier)?.behaviour == ToolboxBehaviour.Portal))
            {
                string destination = entity.Text("Destination", "");
                if (!project.levels.Any(l => l.identifier == destination))
                    throw new FormatException("Porte dans " + level.identifier + " : Destination doit être un nom de niveau existant.");
            }
        return project;
    }
}

[Serializable] public sealed class LdtkWorld { public string identifier; }

[Serializable]
public sealed class LdtkLevel
{
    public string identifier, iid;
    public int pxWid, pxHei;
    public LdtkLayer[] layerInstances;
    public const float PixelsPerUnit = 16f;
    public Rect Bounds => new Rect(0, -pxHei / PixelsPerUnit, pxWid / PixelsPerUnit, pxHei / PixelsPerUnit);
    public LdtkLayer Terrain => layerInstances.First(l => l.__identifier == "Terrain");
    public IEnumerable<LdtkEntity> Entities => layerInstances.Where(l => l.__type == "Entities")
        .SelectMany(l => l.entityInstances ?? Array.Empty<LdtkEntity>());

    // LDtk : origine en haut à gauche, Y descend. Unity : Y monte.
    public Vector3 Position(LdtkEntity entity) => new Vector3(entity.px[0] / PixelsPerUnit, -entity.px[1] / PixelsPerUnit, 0);
    public string EncounterId(LdtkEntity entity) => iid + "/" + entity.iid;

    public void Validate(WorldToolbox toolbox = null)
    {
        if (pxWid < 16 || pxHei < 16 || pxWid % 16 != 0 || pxHei % 16 != 0)
            throw new FormatException("La taille du niveau doit être un multiple de 16 pixels.");
        var terrain = layerInstances?.SingleOrDefault(l => l.__identifier == "Terrain");
        if (terrain == null || terrain.__type != "IntGrid" || terrain.__gridSize != 16 ||
            terrain.__cWid * 16 != pxWid || terrain.__cHei * 16 != pxHei ||
            terrain.intGridCsv == null || terrain.intGridCsv.Length != terrain.__cWid * terrain.__cHei)
            throw new FormatException("Calque Terrain requis : IntGrid de 16 px couvrant tout le niveau.");
        if (terrain.intGridCsv.Any(v => v < 0 || v > 3))
            throw new FormatException("Terrain : 0 = vide bloquant, 1 = mousse, 2 = métal, 3 = mur.");
        foreach (var layer in layerInstances)
            if (layer.__pxTotalOffsetX != 0 || layer.__pxTotalOffsetY != 0 ||
                (layer != terrain && layer.__type != "Entities" && layer.__type != "Tiles" && layer.__type != "AutoLayer"))
                throw new FormatException("Prototype : calques sans décalage uniquement.");
        foreach (var layer in layerInstances)
        {
            if (layer.__gridSize <= 0) throw new FormatException("Taille de grille invalide.");
            foreach (var tile in (layer.gridTiles ?? Array.Empty<LdtkTile>()).Concat(layer.autoLayerTiles ?? Array.Empty<LdtkTile>()))
                if (tile.px == null || tile.px.Length != 2 || tile.src == null || tile.src.Length != 2 || tile.src.Any(v => v < 0))
                    throw new FormatException("Tuile LDtk invalide : " + layer.__identifier);
        }
        var entities = Entities.ToArray();
        if (entities.Count(e => e.__identifier == "PlayerSpawn") != 1)
            throw new FormatException("Place exactement un PlayerSpawn dans LDtk.");
        var ids = new HashSet<string>();
        var supported = new[] { "PlayerSpawn", "Drone", "Sentinel", "Medbay", "Beacon", "Crate", "Rock", "Lamp", "Plant", "Sign", "Portal" };
        foreach (var e in entities)
        {
            if (!supported.Contains(e.__identifier) && toolbox?.Find(e.__identifier) == null) throw new FormatException("Entité non prise en charge : " + e.__identifier);
            if (string.IsNullOrEmpty(e.iid) || !ids.Add(e.iid)) throw new FormatException("IID d'entité absent ou dupliqué.");
            if (e.px == null || e.px.Length != 2) throw new FormatException("Position d'entité invalide.");
            if (e.px[0] < 0 || e.px[1] < 0) throw new FormatException("Position d'entité hors niveau.");
            int x = e.px[0] / 16, y = e.px[1] / 16;
            if (x < 0 || y < 0 || x >= terrain.__cWid || y >= terrain.__cHei || !Walkable(terrain.intGridCsv[y * terrain.__cWid + x]))
                throw new FormatException(e.__identifier + " doit être placé sur un sol praticable.");
        }
    }

    public static bool Walkable(int value) => value == 1 || value == 2;
}

[Serializable]
public sealed class LdtkLayer
{
    public string __identifier, __type;
    public int __gridSize, __cWid, __cHei, __pxTotalOffsetX, __pxTotalOffsetY;
    public int[] intGridCsv;
    public LdtkEntity[] entityInstances;
    public int __tilesetDefUid;
    public float __opacity = 1;
    public bool visible = true;
    public LdtkTile[] gridTiles, autoLayerTiles;
}

[Serializable]
public sealed class LdtkEntity
{
    public string __identifier, iid;
    public int[] px;
    public LdtkNumberField[] fieldInstances;
    public string Text(string key, string fallback = "") => fieldInstances?.FirstOrDefault(f => f.__identifier == key)?.textValue ?? fallback;
    public float Number(string key, float fallback) => fieldInstances?.FirstOrDefault(f => f.__identifier == key)?.numberValue ?? fallback;
}

[Serializable] public sealed class LdtkNumberField { public string __identifier; public float numberValue; public string textValue; }

[Serializable] public sealed class LdtkTile { public int[] px, src; public int f; public float a = 1; }
[Serializable] public sealed class LdtkTexture { public int uid; public Texture2D texture; }
