using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Crée les assets et scènes via l'API Unity ; ne remplace jamais les scènes existantes.</summary>
public static class SandboxSetup
{
    public const string MapPath = "Assets/_Game/Maps/Nacre.ldtk";
    public const string ScenePath = "Assets/_Game/Scenes/Sandbox.unity";
    public const string CombatPath = "Assets/_Game/Scenes/SandboxCombat.unity";
    public const string ArtPath = "Assets/_Game/Data/Sandbox/SandboxArt.asset";

    [MenuItem("Tools/RPG/Bac à sable/1 - Préparer et ouvrir")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Quitte Play avant de préparer le bac à sable."); return; }
        AssetDatabase.Refresh();
        var map = AssetDatabase.LoadAssetAtPath<LdtkMap>(MapPath);
        if (map == null) throw new System.InvalidOperationException("Carte introuvable : " + MapPath);
        map.Read(); // Échouer avant de modifier des scènes si LDtk est invalide.
        var art = PrepareArt();
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("Bac à sable LDtk").AddComponent<SandboxWorld>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, scene);
            root.Configure(map, art);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }
        if (!File.Exists(CombatPath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var battle = new GameObject("Combat").AddComponent<BattleController>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(battle.gameObject, scene);
            battle.Configure(art);
            var camera = new GameObject("Main Camera") { tag = "MainCamera" };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera, scene);
            camera.transform.position = new Vector3(0, 0, -10);
            var cam = camera.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 5.625f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.04f, 0.06f, 0.14f);
            cam.GetUniversalAdditionalCameraData();
            camera.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, CombatPath);
            EditorSceneManager.CloseScene(scene, true);
        }
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (var path in new[] { ScenePath, CombatPath })
        {
            var existing = scenes.FirstOrDefault(s => s.path == path);
            if (existing != null) existing.enabled = true;
            else scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("[Bac à sable] Prêt. Ouvre Sandbox et lance Play. Carte : " + MapPath);
    }

    private static SandboxArt PrepareArt()
    {
        Folder("Assets/_Game/Data/Sandbox");
        var art = AssetDatabase.LoadAssetAtPath<SandboxArt>(ArtPath);
        if (art == null) { art = ScriptableObject.CreateInstance<SandboxArt>(); AssetDatabase.CreateAsset(art, ArtPath); }
        const string spriteRoot = "Assets/_Game/Art/Sprites/";
        art.sprites = AssetDatabase.FindAssets("t:Sprite", new[] { spriteRoot.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".png") && !p.EndsWith("_n.png"))
            .Select(p => new SandboxArt.Entry { key = p.Substring(spriteRoot.Length, p.Length - spriteRoot.Length - 4), sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p) }).ToArray();
        art.lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        art.unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        const string background = "Assets/_Game/Art/Backgrounds/outpost-battle.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(background);
        importer.textureType = TextureImporterType.Default; importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048; importer.SaveAndReimport();
        art.battleBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(background);
        art.drone = Enemy("Drone", "Drone éclaireur", 24, 7, 2, 8, Color.white);
        art.sentinel = Enemy("Sentinel", "Sentinelle", 36, 9, 4, 9, new Color(0.65f, 0.8f, 1));
        if (art.lit == null || art.unlit == null || art.Sprite("Characters/astro_down_0") == null)
            throw new System.InvalidOperationException("Sprites ou matériaux URP manquants.");
        EditorUtility.SetDirty(art);
        return art;
    }

    private static CombatantDefinition Enemy(string file, string name, int hp, int atk, int def, int speed, Color color)
    {
        string path = "Assets/_Game/Data/Sandbox/" + file + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<CombatantDefinition>(path);
        if (definition != null) return definition; // Préserver les ajustements dans l'Inspector.
        definition = ScriptableObject.CreateInstance<CombatantDefinition>();
        definition.displayName = name; definition.maxHp = hp; definition.attack = atk;
        definition.defense = def; definition.speed = speed; definition.placeholderColor = color;
        AssetDatabase.CreateAsset(definition, path);
        return definition;
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    [MenuItem("Tools/RPG/Bac à sable/2 - Ouvrir la carte dans LDtk")]
    public static void OpenMap() => EditorUtility.OpenWithDefaultApp(Path.GetFullPath(MapPath));
}
