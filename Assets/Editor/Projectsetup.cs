using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Script d'éditeur (ne part pas dans le jeu final).
/// Ajoute le menu "Tools > RPG > Créer la structure du projet" qui :
///  - crée l'arborescence de dossiers sous Assets/_Game
///  - crée les scènes Exploration et Combat (copies de la scène 2D du template)
///  - les ajoute à la liste des scènes du build
/// Peut être relancé sans risque : rien n'est écrasé.
/// </summary>
public static class ProjectSetup
{
    private const string Root = "Assets/_Game";

    private static readonly string[] Folders =
    {
        "Scripts/Core",         // code commun (sauvegarde, flags narratifs, utilitaires)
        "Scripts/Combat",       // moteur de combat en C# pur
        "Scripts/Exploration",  // déplacement, interactions sur la carte
        "Scripts/UI",
        "Data/Characters",      // ScriptableObjects des persos
        "Data/Skills",
        "Data/Enemies",
        "Data/Ships",
        "Art/Sprites",
        "Art/Tilesets",
        "Art/Materials",
        "Audio/Music",
        "Audio/SFX",
        "Prefabs",
        "Scenes",
        "Dialogues",
    };

    private static readonly string[] Scenes = { "Exploration", "Combat" };

    [MenuItem("Tools/RPG/Créer la structure du projet")]
    public static void CreateStructure()
    {
        foreach (var folder in Folders)
            Directory.CreateDirectory(Path.Combine(Root, folder));
        AssetDatabase.Refresh();

        var template = FindTemplateScene();
        foreach (var sceneName in Scenes)
            CreateSceneIfMissing(sceneName, template);

        AddScenesToBuild();
        AssetDatabase.SaveAssets();

        // Ouvre la scène d'exploration pour commencer à travailler
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene($"{Root}/Scenes/Exploration.unity");

        Debug.Log("[ProjectSetup] Structure créée sous " + Root);
    }

    // La SampleScene du template Universal 2D contient déjà une caméra 2D et une Global Light 2D
    private static string FindTemplateScene()
    {
        var guid = AssetDatabase.FindAssets("SampleScene t:Scene").FirstOrDefault();
        return guid != null ? AssetDatabase.GUIDToAssetPath(guid) : null;
    }

    private static void CreateSceneIfMissing(string sceneName, string templatePath)
    {
        var path = $"{Root}/Scenes/{sceneName}.unity";
        if (File.Exists(path)) return;

        if (templatePath != null)
        {
            AssetDatabase.CopyAsset(templatePath, path);
        }
        else
        {
            // Pas de SampleScene trouvée : scène vide avec juste une caméra
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var cam = new GameObject("Main Camera", typeof(Camera));
            cam.tag = "MainCamera";
            cam.GetComponent<Camera>().orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AddScenesToBuild()
    {
        var buildScenes = EditorBuildSettings.scenes.ToList();
        foreach (var sceneName in Scenes)
        {
            var path = $"{Root}/Scenes/{sceneName}.unity";
            if (buildScenes.All(s => s.path != path))
                buildScenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
    }
}