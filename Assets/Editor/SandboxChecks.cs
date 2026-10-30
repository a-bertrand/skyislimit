using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Combat;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Vérifications relançables sans dépendance externe. Les tests Play réinitialisent leur propre session.</summary>
[InitializeOnLoad]
public static class SandboxChecks
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static IEnumerator running;
    private static readonly List<string> results = new List<string>();
    private static double deadline;
    private static TestRunnerApi testRunner;
    static SandboxChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("SandboxChecks.Play", false))
            {
                results.Clear(); deadline = EditorApplication.timeSinceStartup + 180;
                running = PlayChecks(); EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) { running = null; EditorApplication.update -= Tick; }
        };
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        results.Add("PASS " + message);
    }

    [MenuItem("Tools/RPG/Bac à sable/3 - Valider la carte et le moteur")]
    public static void Validate()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Quitte Play avant la validation EditMode."); return; }
        results.Clear();
        try
        {
            var map = AssetDatabase.LoadAssetAtPath<LdtkMap>(SandboxSetup.MapPath);
            var level = map.Read().levels[0];
            var terrain = level.Terrain;
            var spawn = level.Entities.Single(e => e.__identifier == "PlayerSpawn");
            var position = level.Position(spawn);
            Check(position.x == spawn.px[0] / 16f && position.y == -spawn.px[1] / 16f, "Conversion LDtk → Unity, Y inversé");
            var reached = new HashSet<int>();
            var queue = new Queue<int>();
            int start = spawn.px[1] / 16 * terrain.__cWid + spawn.px[0] / 16;
            reached.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue(), x = cell % terrain.__cWid, y = cell / terrain.__cWid;
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    int nx = x + direction.x, ny = y + direction.y, next = ny * terrain.__cWid + nx;
                    if (nx < 0 || ny < 0 || nx >= terrain.__cWid || ny >= terrain.__cHei) continue;
                    if (LdtkLevel.Walkable(terrain.intGridCsv[next]) && reached.Add(next)) queue.Enqueue(next);
                }
            }
            foreach (var entity in level.Entities.Where(e => new[] { "Drone", "Sentinel", "Medbay", "Beacon", "Sign" }.Contains(e.__identifier)))
                Check(reached.Contains(entity.px[1] / 16 * terrain.__cWid + entity.px[0] / 16), "Terrain accessible : " + entity.__identifier + " " + entity.iid);
            var data = map.Read(); data.externalLevels = true;
            Reject(JsonUtility.ToJson(data), "Niveaux externes refusés explicitement");
            data = map.Read(); data.levels[0].Terrain.intGridCsv = new int[1];
            Reject(JsonUtility.ToJson(data), "Grille tronquée refusée");
            data = map.Read(); data.levels[0].Entities.First().__identifier = "Unsupported";
            Reject(JsonUtility.ToJson(data), "Entité inconnue refusée");
            data = map.Read(); var entities = data.levels[0].Entities.ToArray(); entities[1].iid = entities[0].iid;
            Reject(JsonUtility.ToJson(data), "Identifiants dupliqués refusés");
            data = map.Read(); data.levels[0].Entities.First(e => e.__identifier == "PlayerSpawn").px = new[] { 0, 0 };
            Reject(JsonUtility.ToJson(data), "Apparition sur vide bloquant refusée");
            var art = AssetDatabase.LoadAssetAtPath<SandboxArt>(SandboxSetup.ArtPath);
            Check(art != null && art.battleBackground != null && art.drone != null && art.sentinel != null, "Ressources de combat embarquées");
            Check(art.Frames("down").Concat(art.Frames("up")).Concat(art.Frames("side")).All(s => s != null), "Neuf images de déplacement présentes");
            Save("Logs/sandbox-editmode.txt");
            testRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            testRunner.RegisterCallbacks(new CombatResults());
            testRunner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "Game.Combat.Tests" } }));
        }
        catch (Exception exception) { Debug.LogException(exception); results.Add("FAIL " + exception); Save("Logs/sandbox-editmode.txt"); }
    }
    private static void Reject(string json, string message)
    {
        try { LdtkProject.Parse(json); }
        catch (FormatException) { Check(true, message); return; }
        throw new Exception(message);
    }
    private sealed class CombatResults : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/sandbox-combat.xml");
            Debug.Log("[Bac à sable] Moteur : " + result.PassCount + " tests réussis, " + result.FailCount + " échecs.");
            Object.DestroyImmediate(testRunner);
        }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }

    [MenuItem("Tools/RPG/Bac à sable/4 - Tester la boucle jouable")]
    public static void RunPlayChecks()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Quitte Play ; ce test démarre sa propre session."); return; }
        SandboxSetup.Prepare();
        SessionState.SetBool("SandboxChecks.Play", true);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout des tests Play.");
            if (running != null && running.MoveNext()) return;
            Finish();
        }
        catch (Exception exception) { results.Add("FAIL " + exception); Debug.LogException(exception); Finish(); }
    }
    private static void Finish()
    {
        EditorApplication.update -= Tick; running = null;
        SessionState.SetBool("SandboxChecks.Play", false);
        Save("Logs/sandbox-playmode.txt");
        EditorApplication.ExitPlaymode();
    }
    private static void Save(string path)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllLines(path, results);
        Debug.Log("[Bac à sable] " + string.Join("\n", results) + "\nRapport : " + path);
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Command(BattleController controller, string name)
    {
        var field = typeof(BattleController).GetField("pending", Private);
        field.SetValue(controller, Enum.Parse(field.FieldType, name));
    }
    private static void MovePlayer(Vector3 position)
    {
        var p = Object.FindAnyObjectByType<PlayerMovement>();
        p.transform.position = position; p.GetComponent<Rigidbody2D>().position = position; Physics2D.SyncTransforms();
    }

    private static IEnumerator PlayChecks()
    {
        // Un MoveNext par mise à jour d'éditeur, en laissant tourner la vraie physique et les coroutines.
        yield return null;
        var world = Object.FindAnyObjectByType<SandboxWorld>();
        Check(world != null && world.Level != null, "Carte reconstruite en Play");
        int total = Object.FindObjectsByType<EncounterTrigger>(FindObjectsSortMode.None).Length;
        Check(total == 3, "Trois rencontres créées depuis LDtk");
        Check(Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Length > 10, "Collisions du décor créées");
        while (Time.timeSinceLevelLoad < 0.5f) yield return null;
        ScreenCapture.CaptureScreenshot("Logs/sandbox-exploration.png");
        yield return null;
        var menu = Object.FindAnyObjectByType<GameMenu>();
        Call(menu, "Open"); Check(GameMenu.IsOpen && Time.timeScale == 0, "Menu : pause");
        Call(menu, "Close"); Check(!GameMenu.IsOpen && Time.timeScale == 1, "Menu : reprise");
        GameSession.Leader.Hp = 1;
        var medbay = world.Level.Entities.First(e => e.__identifier == "Medbay");
        MovePlayer(world.Level.Position(medbay) + Vector3.right);
        yield return null; Call(world, "Update"); Call(world, "Interact");
        Check(GameSession.Leader.Hp == GameSession.Leader.MaxHp, "Soin contextuel depuis la carte");

        // Défaite : contact réel, défense, PV restaurés et retour loin du monstre.
        GameSession.Leader.Hp = 1;
        while (Time.timeSinceLevelLoad < 1.2f) yield return null;
        var encounter = Object.FindObjectsByType<EncounterTrigger>(FindObjectsSortMode.None).First();
        string lostId = (string)typeof(EncounterTrigger).GetProperty("Id", Private).GetValue(encounter);
        MovePlayer(encounter.transform.position);
        while (SceneManager.GetActiveScene().name != "SandboxCombat" || SceneTransition.IsBusy) yield return null;
        var controller = Object.FindAnyObjectByType<BattleController>();
        Check(GameSession.PendingEncounterId == lostId, "Contact physique → scène Combat");
        while (!Field<Battle>(controller, "battle").IsOver || Field<bool>(controller, "busy"))
        {
            if (!Field<bool>(controller, "busy")) Command(controller, "Defend");
            yield return null;
        }
        Check(Field<Battle>(controller, "battle").Winner == Team.Enemy, "Défaite jouable après une garde");
        Command(controller, "Return");
        while (SceneManager.GetActiveScene().name != "Sandbox" || SceneTransition.IsBusy) yield return null;
        Check(!GameSession.IsDefeated(lostId) && GameSession.Leader.Hp == 40, "Défaite : ennemi conservé et équipe soignée");
        world = Object.FindAnyObjectByType<SandboxWorld>();
        var spawn = world.Level.Position(world.Level.Entities.Single(e => e.__identifier == "PlayerSpawn"));
        Check(Vector3.Distance(Object.FindAnyObjectByType<PlayerMovement>().transform.position, spawn) < 0.1f, "Défaite : retour au point de départ");

        for (int n = 0; n < total; n++)
        {
            while (Time.timeSinceLevelLoad < 1.2f) yield return null;
            encounter = Object.FindObjectsByType<EncounterTrigger>(FindObjectsSortMode.None).First();
            string id = (string)typeof(EncounterTrigger).GetProperty("Id", Private).GetValue(encounter);
            GameSession.Leader.Hp = 40;
            var returnPosition = encounter.transform.position;
            MovePlayer(returnPosition);
            while (SceneManager.GetActiveScene().name != "SandboxCombat" || SceneTransition.IsBusy) yield return null;
            controller = Object.FindAnyObjectByType<BattleController>();
            if (n == 0) { ScreenCapture.CaptureScreenshot("Logs/sandbox-battle.png"); yield return null; }
            while (!Field<Battle>(controller, "battle").IsOver || Field<bool>(controller, "busy"))
            {
                if (!Field<bool>(controller, "busy")) Command(controller, "Attack");
                yield return null;
            }
            Check(Field<Battle>(controller, "battle").Winner == Team.Player, "Victoire animée " + (n + 1));
            int remainingHp = Field<Combatant>(controller, "heroCombatant").Hp;
            Command(controller, "Return");
            while (SceneManager.GetActiveScene().name != "Sandbox" || SceneTransition.IsBusy) yield return null;
            Check(GameSession.IsDefeated(id) && Object.FindObjectsByType<EncounterTrigger>(FindObjectsSortMode.None).Length == total - n - 1, "Ennemi vaincu absent au retour " + (n + 1));
            Check(GameSession.Leader.Hp == remainingHp && remainingHp < 40, "PV persistants après victoire " + (n + 1));
            Check(Vector3.Distance(Object.FindAnyObjectByType<PlayerMovement>().transform.position, returnPosition) < 0.2f, "Position conservée après victoire " + (n + 1));
        }
        world = Object.FindAnyObjectByType<SandboxWorld>();
        var beacon = world.Level.Entities.Single(e => e.__identifier == "Beacon");
        MovePlayer(world.Level.Position(beacon) + Vector3.down);
        yield return null; Call(world, "Update"); Call(world, "Interact");
        Check(GameSession.BeaconActivated, "Objectif terminé par interaction avec la balise");
        GameSession.RestartSandbox();
        while (SceneTransition.IsBusy) yield return null;
        Check(!GameSession.BeaconActivated && GameSession.Leader.Hp == 40 && Object.FindObjectsByType<EncounterTrigger>(FindObjectsSortMode.None).Length == 3, "Rejouer restaure la carte, les ennemis et les PV");
    }
}
