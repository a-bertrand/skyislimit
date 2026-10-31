using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ToolboxDemo
{
    public const string MapPath = "Assets/_Game/Maps/Atelier.ldtk";
    public const string ScenePath = "Assets/_Game/Scenes/Atelier.unity";
    [MenuItem("Tools/RPG/Ouvrir l'atelier jouable")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Quitte Play avant de préparer l'atelier.");
        var catalog = WorldToolboxWindow.InitializeAssets();
        if (!File.Exists(ScenePath))
        {
            if (catalog.Find("DroneCuivre") == null)
            {
                var combat = UnityEngine.Object.Instantiate(catalog.Find("Drone").combat);
                combat.displayName = "Drone cuivre"; combat.maxHp = 18; combat.attack = 5; combat.defense = 2;
                AssetDatabase.CreateAsset(combat, AssetDatabase.GenerateUniqueAssetPath("Assets/_Game/Data/Sandbox/DroneCuivre.asset"));
                var sample = new WorldToolbox.Entry { id = "DroneCuivre", sprite = catalog.Find("Drone").sprite,
                    behaviour = ToolboxBehaviour.Enemy, combat = combat, tint = new Color(1, .65f, .3f), hover = true, patrolRadius = 16, speed = .5f };
                catalog.entries = catalog.entries.Concat(new[] { sample }).ToArray();
                EditorUtility.SetDirty(catalog);
            }
            string json = ToolboxSync.Synchronize(File.ReadAllText(MapPath), MapPath, catalog);
            var project = LdtkProject.Parse(json, catalog);
            if (!project.levels.Any(l => l.identifier == "Laboratoire"))
            {
                json = ToolboxSync.AddLevel(json, "Laboratoire");
                json = ToolboxSync.AddTileLayer(json, MapPath, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Sprites/Tiles/moss_0.png"), 16);
                var doc = ToolboxJson.Object(json);
                ToolboxChecks.AddEntity(doc, "Laboratoire", "Portal", project.levels[0].identifier, 72, 40);
                var spawn = project.levels[0].Entities.Single(e => e.__identifier == "PlayerSpawn");
                ToolboxChecks.AddEntity(doc, project.levels[0].identifier, "Portal", "Laboratoire", spawn.px[0] + 32, spawn.px[1] - 16);
                ToolboxChecks.AddEntity(doc, "Laboratoire", "DroneCuivre", "", 168, 104);
                ToolboxChecks.AddEntity(doc, "Laboratoire", "Medbay", "", 40, 104);
                ToolboxChecks.AddEntity(doc, "Laboratoire", "Beacon", "", 232, 104);
                ToolboxChecks.AddEntity(doc, "Laboratoire", "Sign", "", 40, 72);
                var lab = doc["levels"].List().First(l => (string)l.Obj()["identifier"] == "Laboratoire").Obj();
                var layer = lab["layerInstances"].List().First(l => (string)l.Obj()["__type"] == "Tiles").Obj();
                for (int x = 5; x < 15; x++)
                    layer["gridTiles"].List().Add(new System.Collections.Generic.Dictionary<string, object> {
                        { "px", new object[] { x * 16, 128 } }, { "src", new object[] { 0, 0 } }, { "f", 0 }, { "t", 0 }, { "d", new object[] { 8 * Convert.ToInt32(layer["__cWid"]) + x } }, { "a", 1 }
                    });
                json = ToolboxJson.Write(doc);
            }
            LdtkProject.Parse(json, catalog);
            foreach (var e in catalog.entries.Where(e => e.combat != null))
            {
                e.combat.battleSprite = e.sprite; e.combat.placeholderColor = e.tint;
                e.combat.idleFrames = e.animation; e.combat.animationFps = e.framesPerSecond; EditorUtility.SetDirty(e.combat);
            }
            AssetDatabase.SaveAssets(); ToolboxSync.Save(MapPath, json);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var world = new GameObject("Atelier LDtk").AddComponent<SandboxWorld>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(world.gameObject, scene);
            world.Configure(AssetDatabase.LoadAssetAtPath<LdtkMap>(MapPath), AssetDatabase.LoadAssetAtPath<SandboxArt>(SandboxSetup.ArtPath));
            var so = new SerializedObject(world); so.FindProperty("startingLevel").stringValue = "Laboratoire"; so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath); EditorSceneManager.CloseScene(scene, true);
        }
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) EditorSceneManager.OpenScene(ScenePath);
        else Debug.Log("Atelier prêt : enregistre ta scène puis ouvre " + ScenePath);
        WorldToolboxWindow.Open();
    }
}
