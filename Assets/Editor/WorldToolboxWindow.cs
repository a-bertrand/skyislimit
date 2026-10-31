using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class WorldToolboxWindow : EditorWindow
{
    private WorldToolbox catalog;
    private LdtkMap map;
    private SerializedObject serialized;
    private SerializedObject combatSerialized;
    private readonly ToolboxLevelList levels = new ToolboxLevelList();
    private readonly HashSet<UnityEngine.Object> pendingSaves = new HashSet<UnityEngine.Object>();
    private double saveAt;
    private Vector2 scroll;
    private int selected;
    private string levelName = "NouvelleCarte", status;
    private Texture2D tileset;
    private int tileSize = 16;
    private string diskSnapshot;
    private LdtkMap lastMap;

    [MenuItem("Tools/RPG/Boîte à outils")]
    public static void Open()
    {
        var window = GetWindow<WorldToolboxWindow>("Boîte à outils");
        window.LoadDefaults(); window.Repaint();
    }
    private void OnEnable()
    {
        LoadDefaults();
        EditorApplication.update += SaveWhenIdle;
    }
    private void LoadDefaults()
    {
        if (catalog == null) catalog = AssetDatabase.LoadAssetAtPath<WorldToolbox>(LdtkMapImporter.ToolboxPath);
        minSize = new Vector2(480, 650);
        if (map == null) map = AssetDatabase.LoadAssetAtPath<LdtkMap>("Assets/_Game/Maps/Atelier.ldtk") ?? AssetDatabase.LoadAssetAtPath<LdtkMap>(SandboxSetup.MapPath);
    }
    private void OnDisable()
    {
        EditorApplication.update -= SaveWhenIdle;
        FlushPendingSaves();
        serialized?.Dispose(); serialized = null;
        combatSerialized?.Dispose(); combatSerialized = null;
    }
    private void ScheduleSave(UnityEngine.Object asset)
    {
        pendingSaves.Add(asset);
        saveAt = EditorApplication.timeSinceStartup + 1;
    }
    private void SaveWhenIdle()
    {
        // Un seul enregistrement après la saisie, jamais à chaque pas d'un slider.
        if (pendingSaves.Count == 0 || EditorApplication.timeSinceStartup < saveAt ||
            GUIUtility.hotControl != 0 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        FlushPendingSaves();
    }
    private void FlushPendingSaves()
    {
        if (pendingSaves.Count == 0) return;
        var assets = pendingSaves.ToArray(); pendingSaves.Clear();
        foreach (var asset in assets) if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
    }
    private void Run(Action action)
    {
        try { action(); status = "Terminé. " + DateTime.Now.ToString("HH:mm:ss"); }
        catch (Exception e) { status = e.Message; Debug.LogWarning("[Boîte à outils] " + e.Message); }
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bibliothèque du jeu", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Crée les types d'objets ici ; place leurs exemplaires dans LDtk. Enregistre et ferme le projet LDtk avant de synchroniser, puis rouvre-le pour voir les changements.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (catalog == null)
            {
                if (GUILayout.Button("Initialiser la boîte à outils et une copie Atelier")) Run(Initialize);
                return;
            }
            catalog = (WorldToolbox)EditorGUILayout.ObjectField("Catalogue", catalog, typeof(WorldToolbox), false);
            if (catalog == null) return;
            map = (LdtkMap)EditorGUILayout.ObjectField("Projet LDtk", map, typeof(LdtkMap), false);
            if (map != lastMap) { lastMap = map; diskSnapshot = map != null ? map.json : null; }
            if (GUILayout.Button("Synchroniser la bibliothèque vers LDtk")) Run(() => ChangeMap(s => ToolboxSync.Synchronize(s, AssetDatabase.GetAssetPath(map), catalog)));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ouvrir dans LDtk") && map != null) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetDatabase.GetAssetPath(map)));
            if (GUILayout.Button("Vérifier le projet")) Run(() => { catalog.Validate(); map.Read(); });
            EditorGUILayout.EndHorizontal();
            if (map != null && GUILayout.Button("Copier le chemin du projet LDtk"))
                EditorGUIUtility.systemCopyBuffer = Path.GetFullPath(AssetDatabase.GetAssetPath(map));
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawEntries();
            EditorGUILayout.Space(16);
            EditorGUILayout.LabelField("Décors PNG", EditorStyles.boldLabel);
            tileset = (Texture2D)EditorGUILayout.ObjectField("Planche PNG", tileset, typeof(Texture2D), false);
            tileSize = EditorGUILayout.IntField("Taille des cases (px)", tileSize);
            if (GUILayout.Button("Importer un PNG depuis le disque")) Run(ImportPng);
            using (new EditorGUI.DisabledScope(tileset == null))
                if (GUILayout.Button("Ajouter ce tileset et son calque dans LDtk")) Run(() => ChangeMap(s => ToolboxSync.AddTileLayer(s, AssetDatabase.GetAssetPath(map), tileset, tileSize)));
            EditorGUILayout.Space(16);
            EditorGUILayout.LabelField("Cartes du projet", EditorStyles.boldLabel);
            levelName = EditorGUILayout.TextField("Nom de la nouvelle carte", levelName);
            if (GUILayout.Button("Créer une carte vide avec un départ")) Run(() => ChangeMap(s => ToolboxSync.AddLevel(s, levelName)));
            if (map != null)
            {
                levels.Refresh(map);
                if (levels.Error != null) EditorGUILayout.HelpBox(levels.Error, MessageType.Warning);
                foreach (var name in levels.Names)
                {
                    EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField(name);
                    if (GUILayout.Button("Choisir pour Play", GUILayout.Width(140))) Run(() => SelectLevel(name));
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.HelpBox("Passages : place Portal dans LDtk et renseigne Destination avec le nom exact d'une carte. E transporte le joueur à son PlayerSpawn. Les collisions restent dessinées dans Terrain (0/3 bloquent ; 1/2 sont praticables).", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
    private void DrawEntries()
    {
        if (serialized == null || serialized.targetObject != catalog)
        {
            serialized?.Dispose(); serialized = new SerializedObject(catalog);
        }
        serialized.UpdateIfRequiredOrScript();
        var entries = serialized.FindProperty("entries");
        if (entries.arraySize > 0)
        {
            selected = Mathf.Clamp(selected, 0, entries.arraySize - 1);
            selected = EditorGUILayout.Popup("Élément", selected, catalog.entries.Select(e => e.id).ToArray());
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ajouter"))
        {
            Undo.RecordObject(catalog, "Ajouter un élément");
            var list = catalog.entries.ToList();
            string id = "NouvelObjet"; int n = 1; while (catalog.Find(id) != null) id = "NouvelObjet" + n++;
            list.Add(new WorldToolbox.Entry { id = id, behaviour = ToolboxBehaviour.Decoration });
            catalog.entries = list.ToArray(); selected = list.Count - 1; EditorUtility.SetDirty(catalog); ScheduleSave(catalog); serialized.Update();
        }
        using (new EditorGUI.DisabledScope(entries.arraySize == 0))
        {
            if (GUILayout.Button("Dupliquer"))
            {
                Undo.RecordObject(catalog, "Dupliquer un élément");
                var entry = JsonUtility.FromJson<WorldToolbox.Entry>(JsonUtility.ToJson(catalog.entries[selected]));
                string stem = entry.id + "Copie"; entry.id = stem; int n = 1; while (catalog.Find(entry.id) != null) entry.id = stem + n++;
                if (entry.combat != null)
                {
                    var copy = Instantiate(entry.combat);
                    AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath("Assets/_Game/Data/Sandbox/" + entry.id + ".asset")); entry.combat = copy;
                }
                catalog.entries = catalog.entries.Concat(new[] { entry }).ToArray(); selected = catalog.entries.Length - 1;
                EditorUtility.SetDirty(catalog); ScheduleSave(catalog); serialized.Update();
            }
            if (GUILayout.Button("Retirer")) Run(() =>
            {
                string id = catalog.entries[selected].id;
                // Inspecte tous les fichiers LDtk du projet avant de retirer une définition partagée.
                foreach (string path in Directory.GetFiles("Assets", "*.ldtk", SearchOption.AllDirectories))
                    if (ToolboxSync.Entities(ToolboxJson.Object(File.ReadAllText(path))).Any(e => (string)e["__identifier"] == id))
                        throw new InvalidOperationException(id + " est encore utilisé dans " + path + ". Retire d'abord ses placements dans LDtk.");
                Undo.RecordObject(catalog, "Retirer un élément");
                catalog.entries = catalog.entries.Where((e, i) => i != selected).ToArray(); selected = 0;
                EditorUtility.SetDirty(catalog); ScheduleSave(catalog); serialized.Update();
            });
        }
        EditorGUILayout.EndHorizontal();
        if (entries.arraySize > 0)
        {
            selected = Mathf.Clamp(selected, 0, entries.arraySize - 1);
            var entry = entries.GetArrayElementAtIndex(selected);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("id"), new GUIContent("Identifiant LDtk (stable)"));
            EditorGUILayout.HelpBox("Une fois placé, garde cet identifiant. Duplique l'élément pour créer une variante.", MessageType.None);
            var behaviour = entry.FindPropertyRelative("behaviour");
            int nextBehaviour = EditorGUILayout.Popup("Comportement", behaviour.enumValueIndex,
                new[] { "Monstre", "Décor", "Soin", "Balise", "Terminal", "Porte" });
            if (nextBehaviour != behaviour.enumValueIndex) behaviour.enumValueIndex = nextBehaviour;
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("sprite"), new GUIContent("Sprite"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("tint"), new GUIContent("Teinte"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("hover"), new GUIContent("Flottement animé"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("lightIntensity"), new GUIContent("Intensité lumineuse"));
            if (entry.FindPropertyRelative("lightIntensity").floatValue > 0)
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("lightColor"), new GUIContent("Couleur de lumière"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("lightRadius"), new GUIContent("Rayon lumineux"));
            }
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("animation"), new GUIContent("Images d'animation"), true);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("framesPerSecond"), new GUIContent("Images par seconde"));
            var kind = (ToolboxBehaviour)entry.FindPropertyRelative("behaviour").enumValueIndex;
            if (kind == ToolboxBehaviour.Enemy)
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("patrolRadius"), new GUIContent("Rayon par défaut (px)"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("speed"), new GUIContent("Vitesse par défaut"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("combat"), new GUIContent("Fiche de combat"));
            }
            else
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("solid"), new GUIContent("Obstacle solide"));
                if (entry.FindPropertyRelative("solid").boolValue) EditorGUILayout.PropertyField(entry.FindPropertyRelative("colliderSize"), new GUIContent("Taille collision (unités)"));
                if (kind == ToolboxBehaviour.Sign) EditorGUILayout.PropertyField(entry.FindPropertyRelative("text"), new GUIContent("Texte par défaut"));
            }
            if (serialized.ApplyModifiedProperties()) ScheduleSave(catalog);
            var current = catalog.entries[selected];
            if (current.behaviour == ToolboxBehaviour.Enemy)
            {
                if (current.combat == null && GUILayout.Button("Créer sa fiche de combat"))
                {
                    Undo.RecordObject(catalog, "Créer une fiche");
                    current.combat = CreateInstance<CombatantDefinition>(); current.combat.displayName = current.id;
                    AssetDatabase.CreateAsset(current.combat, AssetDatabase.GenerateUniqueAssetPath("Assets/_Game/Data/Sandbox/Monstre.asset"));
                    EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
                }
                if (current.combat != null)
                {
                    EditorGUILayout.LabelField("Caractéristiques de combat", EditorStyles.boldLabel);
                    if (combatSerialized == null || combatSerialized.targetObject != current.combat)
                    {
                        combatSerialized?.Dispose(); combatSerialized = new SerializedObject(current.combat);
                    }
                    var stats = combatSerialized; stats.UpdateIfRequiredOrScript();
                    EditorGUILayout.PropertyField(stats.FindProperty("displayName"), new GUIContent("Nom affiché"));
                    EditorGUILayout.PropertyField(stats.FindProperty("maxHp"), new GUIContent("PV maximum"));
                    EditorGUILayout.PropertyField(stats.FindProperty("attack"), new GUIContent("Attaque"));
                    EditorGUILayout.PropertyField(stats.FindProperty("defense"), new GUIContent("Défense"));
                    EditorGUILayout.PropertyField(stats.FindProperty("speed"), new GUIContent("Vitesse de combat"));
                    if (stats.ApplyModifiedProperties()) ScheduleSave(current.combat);
                }
            }
        }
    }
    private void ChangeMap(Func<string, string> change)
    {
        if (map == null) throw new InvalidOperationException("Choisis un projet LDtk.");
        FlushPendingSaves();
        string path = AssetDatabase.GetAssetPath(map), current = File.ReadAllText(path);
        if (diskSnapshot != null && current != diskSnapshot)
        {
            diskSnapshot = current;
            throw new InvalidOperationException("Le fichier a changé depuis LDtk. Version disque relue : vérifie tes changements puis relance l'action.");
        }
        string result = change(current);
        LdtkProject.Parse(result, catalog); // Valider avant d'écrire, y compris les destinations.
        foreach (var entry in catalog.entries.Where(e => e.behaviour == ToolboxBehaviour.Enemy && e.combat != null))
        {
            entry.combat.battleSprite = entry.sprite; entry.combat.idleFrames = entry.animation;
            entry.combat.animationFps = entry.framesPerSecond; entry.combat.placeholderColor = entry.tint;
            EditorUtility.SetDirty(entry.combat);
        }
        AssetDatabase.SaveAssets(); ToolboxSync.Save(path, result); diskSnapshot = result;
        var importer = (LdtkMapImporter)AssetImporter.GetAtPath(path);
        if (importer.toolbox != catalog) { importer.toolbox = catalog; importer.SaveAndReimport(); }
    }
    private void SelectLevel(string name)
    {
        var world = FindAnyObjectByType<SandboxWorld>();
        if (world == null) throw new InvalidOperationException("Ouvre la scène Sandbox avant de choisir le niveau de départ.");
        var so = new SerializedObject(world); so.FindProperty("map").objectReferenceValue = map;
        so.FindProperty("startingLevel").stringValue = name; so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        Selection.activeObject = world; // L'utilisateur enregistre sa scène, sans écraser d'autres changements.
    }
    private void ImportPng()
    {
        string source = EditorUtility.OpenFilePanel("Importer un sprite ou tileset PNG", "", "png");
        if (string.IsNullOrEmpty(source)) return;
        string folder = "Assets/_Game/Art/Toolbox";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game/Art", "Toolbox");
        string target = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(source));
        File.Copy(source, target); AssetDatabase.ImportAsset(target);
        var importer = (TextureImporter)AssetImporter.GetAtPath(target);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16; importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        tileset = AssetDatabase.LoadAssetAtPath<Texture2D>(target); Selection.activeObject = tileset;
    }
    public void Initialize()
    {
        catalog = InitializeAssets();
        map = AssetDatabase.LoadAssetAtPath<LdtkMap>("Assets/_Game/Maps/Atelier.ldtk");
    }
    [MenuItem("Tools/RPG/Initialiser la boîte à outils")]
    public static void InitializeMenu() { InitializeAssets(); Open(); }

    public static WorldToolbox InitializeAssets()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WorldToolbox>(LdtkMapImporter.ToolboxPath);
        if (catalog == null)
        {
            var art = AssetDatabase.LoadAssetAtPath<SandboxArt>(SandboxSetup.ArtPath);
            if (art == null) throw new InvalidOperationException("Prépare d'abord le bac à sable via Tools/RPG/Bac à sable.");
            catalog = CreateInstance<WorldToolbox>();
            catalog.entries = new[] {
                Entry("Drone", ToolboxBehaviour.Enemy, art.Sprite("Characters/drone"), art.drone),
                Entry("Sentinel", ToolboxBehaviour.Enemy, art.Sprite("Characters/drone"), art.sentinel),
                Entry("Crate", ToolboxBehaviour.Decoration, art.Sprite("Props/crate")),
                Entry("Rock", ToolboxBehaviour.Decoration, art.Sprite("Props/rock_0")),
                Entry("Lamp", ToolboxBehaviour.Decoration, art.Sprite("Props/lamp")),
                Entry("Plant", ToolboxBehaviour.Decoration, art.Sprite("Props/glowplant")),
                Entry("Medbay", ToolboxBehaviour.Heal, art.Sprite("Props/beacon")),
                Entry("Beacon", ToolboxBehaviour.Beacon, art.Sprite("Props/beacon")),
                Entry("Sign", ToolboxBehaviour.Sign, art.Sprite("Props/pillar")),
                Entry("Portal", ToolboxBehaviour.Portal, art.Sprite("Props/beacon"))
            };
            catalog.Find("Sentinel").tint = new Color(.65f, .8f, 1);
            catalog.Find("Medbay").tint = new Color(.6f, 1, .75f);
            catalog.Find("Portal").tint = new Color(1, .6f, 1);
            catalog.Find("Drone").hover = true; catalog.Find("Sentinel").hover = true;
            catalog.Find("Lamp").lightIntensity = 1.2f; catalog.Find("Lamp").lightColor = new Color(1, .75f, .3f);
            catalog.Find("Plant").lightIntensity = .6f;
            catalog.Find("Sign").text = "Explore la carte. Contact ennemi = combat ; E = interagir.";
            foreach (string id in new[] { "Crate", "Rock", "Lamp", "Sign", "Medbay", "Beacon" }) catalog.Find(id).solid = true;
            AssetDatabase.CreateAsset(catalog, LdtkMapImporter.ToolboxPath);
        }
        if (catalog.formatVersion == 0)
        {
            if (catalog.Find("Drone") != null) catalog.Find("Drone").hover = true;
            if (catalog.Find("Sentinel") != null) catalog.Find("Sentinel").hover = true;
            if (catalog.Find("Lamp") != null) { catalog.Find("Lamp").lightIntensity = 1.2f; catalog.Find("Lamp").lightColor = new Color(1, .75f, .3f); }
            if (catalog.Find("Plant") != null) catalog.Find("Plant").lightIntensity = .6f;
            catalog.formatVersion = 1; EditorUtility.SetDirty(catalog);
        }
        const string atelier = "Assets/_Game/Maps/Atelier.ldtk";
        if (!File.Exists(atelier))
        {
            if (!AssetDatabase.CopyAsset(SandboxSetup.MapPath, atelier)) throw new IOException("Impossible de créer la copie Atelier.");
            ToolboxSync.Save(atelier, ToolboxSync.Synchronize(File.ReadAllText(atelier), atelier, catalog));
        }
        AssetDatabase.SaveAssets();
        foreach (string path in Directory.GetFiles("Assets/_Game/Maps", "*.ldtk")) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return catalog;
    }
    private static WorldToolbox.Entry Entry(string id, ToolboxBehaviour kind, Sprite sprite, CombatantDefinition combat = null) =>
        new WorldToolbox.Entry { id = id, behaviour = kind, sprite = sprite, combat = combat };
}
