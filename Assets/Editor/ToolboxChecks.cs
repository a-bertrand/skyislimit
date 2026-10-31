using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ToolboxChecks
{
    [MenuItem("Tools/RPG/Tester la boîte à outils")]
    public static void Run()
    {
        var report = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); report.Add("PASS " + name); }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (FormatException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, name); }
        WorldToolbox clone = null;
        try
        {
            var catalog = WorldToolboxWindow.InitializeAssets();
            const string path = "Assets/_Game/Maps/Atelier.ldtk";
            string original = File.ReadAllText(path);
            var before = ToolboxJson.Object(original);
            string synced = ToolboxSync.Synchronize(original, path, catalog);
            var after = ToolboxJson.Object(synced);
            Check((string)after["iid"] == (string)before["iid"], "Identité du projet conservée");
            Check(ToolboxSync.Entities(before).Select(e => (string)e["iid"]).SequenceEqual(ToolboxSync.Entities(after).Select(e => (string)e["iid"])), "Placements et identifiants conservés");
            Check(ToolboxJson.Write(before["levels"].List()[0].Obj()["layerInstances"].List().First(l => (string)l.Obj()["__identifier"] == "Terrain")) == ToolboxJson.Write(after["levels"].List()[0].Obj()["layerInstances"].List().First(l => (string)l.Obj()["__identifier"] == "Terrain")), "Peinture du terrain conservée");
            Check(ToolboxSync.Synchronize(synced, path, catalog) == synced, "Synchronisation idempotente");
            clone = UnityEngine.Object.Instantiate(catalog);
            var custom = new WorldToolbox.Entry { id = "ScarabeeTest", sprite = catalog.Find("Drone").sprite, combat = catalog.Find("Drone").combat, behaviour = ToolboxBehaviour.Enemy };
            clone.entries = clone.entries.Concat(new[] { custom }).ToArray();
            string added = ToolboxSync.Synchronize(synced, path, clone);
            Check(ToolboxJson.Object(added)["defs"].Obj()["entities"].List().Any(e => (string)e.Obj()["identifier"] == custom.id), "Ajout d'un monstre dans la palette LDtk");
            Check(!ToolboxJson.Object(ToolboxSync.Synchronize(added, path, catalog))["defs"].Obj()["entities"].List().Any(e => (string)e.Obj()["identifier"] == custom.id), "Retrait d'une définition inutilisée");
            clone.entries = clone.entries.Where(e => e.id != "Drone").ToArray();
            Reject(() => ToolboxSync.Synchronize(synced, path, clone), "Suppression d'un monstre placé refusée");
            string two = ToolboxSync.AddLevel(synced, "LaboratoireTest");
            var project = LdtkProject.Parse(two, catalog);
            Check(project.levels.Length == LdtkProject.Parse(synced, catalog).levels.Length + 1, "Ajout d'une carte au même projet");
            Check(project.levels.Last().Entities.Count() == 1 && project.levels.Last().Entities.Single().__identifier == "PlayerSpawn", "Carte vide avec départ valide");
            Reject(() => ToolboxSync.AddLevel(two, "LaboratoireTest"), "Nom de carte dupliqué refusé");
            string tiled = ToolboxSync.AddTileLayer(two, path, catalog.Find("Drone").sprite.texture, 16);
            Check(LdtkProject.Parse(tiled, catalog).levels.All(l => l.layerInstances.Any(a => a.__type == "Tiles")), "Calque PNG ajouté à chaque niveau");
            var tree = ToolboxJson.Object(two);
            AddEntity(tree, "LaboratoireTest", "Portal", project.levels[0].identifier, 72, 40);
            string linked = ToolboxJson.Write(tree);
            Check(LdtkProject.Parse(linked, catalog).levels.Last().Entities.First(e => e.__identifier == "Portal").Text("Destination") == project.levels[0].identifier, "Lecture des destinations texte");
            var portal = ToolboxSync.Entities(tree).Last(e => (string)e["__identifier"] == "Portal");
            Check((string)portal["fieldInstances"].List()[0].Obj()["realEditorValues"].List()[0].Obj()["params"].List()[0] == project.levels[0].identifier, "Destination conservée à la réouverture dans LDtk");
            portal["fieldInstances"].List()[0].Obj()["__value"] = "Inconnue";
            Reject(() => LdtkProject.Parse(ToolboxJson.Write(tree), catalog), "Destination inexistante refusée");
            var raw = ToolboxJson.Object("{\"text\":\"Étoile \\n \\u2605 \\\" \\\\\",\"n\":-1.25e2,\"a\":[true,false,null]}");
            Check(ToolboxJson.Write(ToolboxJson.Object(ToolboxJson.Write(raw))) == ToolboxJson.Write(raw), "JSON Unicode, échappements, nombres et valeurs nulles");
            Check(File.ReadAllText(path) == original, "Tests de transformation sans écriture sur la carte");
            report.Add("SUCCESS " + report.Count + " contrôles");
            Debug.Log("[Boîte à outils] " + report.Last());
        }
        catch (Exception e) { report.Add("FAIL " + e); Debug.LogException(e); }
        finally
        {
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/toolbox-checks.txt", report);
        }
    }

    public static void AddEntity(Dictionary<string, object> doc, string levelName, string type, string destination, int x, int y)
    {
        var level = doc["levels"].List().Select(l => l.Obj()).First(l => (string)l["identifier"] == levelName);
        var layer = level["layerInstances"].List().Select(l => l.Obj()).First(l => (string)l["__type"] == "Entities");
        var template = ToolboxSync.Entities(doc).First();
        var instance = ToolboxJson.Object(ToolboxJson.Write(template));
        var def = doc["defs"].Obj()["entities"].List().Select(e => e.Obj()).First(e => (string)e["identifier"] == type);
        instance["iid"] = Guid.NewGuid().ToString(); instance["__identifier"] = type; instance["defUid"] = def["uid"];
        instance["px"] = new object[] { x, y }; instance["__grid"] = new object[] { x / 16, y / 16 };
        instance["__pivot"] = new object[] { def["pivotX"], def["pivotY"] }; instance["__tile"] = def["tileRect"];
        instance["width"] = def["width"]; instance["height"] = def["height"];
        instance["__tags"] = def["tags"];
        instance["fieldInstances"] = def["fieldDefs"].List().Select(f => (object)new Dictionary<string, object> {
            { "__identifier", f.Obj()["identifier"] }, { "__type", f.Obj()["__type"] }, { "defUid", f.Obj()["uid"] },
            { "__value", (string)f.Obj()["identifier"] == "Destination" ? (object)destination : f.Obj()["defaultOverride"]?.Obj()["params"].List()[0] },
            { "__tile", null },
            { "realEditorValues", new object[] { new Dictionary<string, object> {
                { "id", "V_" + (string)f.Obj()["__type"] },
                { "params", new object[] { (string)f.Obj()["identifier"] == "Destination" ? (object)destination : f.Obj()["defaultOverride"]?.Obj()["params"].List()[0] } }
            } } }
        }).ToList();
        layer["entityInstances"].List().Add(instance);
    }
}
