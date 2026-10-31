using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Combat;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ToolboxPlayChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double deadline;
    private static readonly List<string> results = new List<string>();
    static ToolboxPlayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ToolboxChecks.Play", false))
            {
                results.Clear(); deadline = EditorApplication.timeSinceStartup + 120;
                routine = Run(); EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) { routine = null; EditorApplication.update -= Tick; }
        };
    }
    [MenuItem("Tools/RPG/Tester les cartes et passages en jeu")]
    public static void Start()
    {
        if (EditorApplication.isPlaying) return;
        if (SceneManager.GetActiveScene().isDirty) { Debug.LogWarning("Enregistre la scène avant le test."); return; }
        ToolboxDemo.Open();
        SessionState.SetBool("ToolboxChecks.Play", true); EditorApplication.EnterPlaymode();
    }
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); results.Add("PASS " + label); }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout des tests de cartes.");
            if (routine.MoveNext()) return;
            Finish();
        }
        catch (Exception e) { results.Add("FAIL " + e); Debug.LogException(e); Finish(); }
    }
    private static void Finish()
    {
        EditorApplication.update -= Tick; routine = null; SessionState.SetBool("ToolboxChecks.Play", false);
        Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/toolbox-playmode.txt", results);
        Debug.Log("[Atelier] " + string.Join("\n", results)); EditorApplication.ExitPlaymode();
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Move(Vector3 position)
    {
        var player = Object.FindAnyObjectByType<PlayerMovement>(); player.transform.position = position;
        player.GetComponent<Rigidbody2D>().position = position; Physics2D.SyncTransforms();
    }
    private static void Command(BattleController battle, string value)
    {
        var field = typeof(BattleController).GetField("pending", Private); field.SetValue(battle, Enum.Parse(field.FieldType, value));
    }
    private static IEnumerator Run()
    {
        yield return null;
        var world = Object.FindAnyObjectByType<SandboxWorld>();
        Check(world != null && world.Level.identifier == "Laboratoire", "Niveau de départ sélectionné");
        Check(Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Count(s => s.name.Contains("tuile")) == 10, "Dix tuiles PNG construites depuis LDtk");
        var encounter = Object.FindAnyObjectByType<EncounterTrigger>();
        Check(encounter != null && Field<CombatantDefinition>(encounter, "enemy").displayName == "Drone cuivre", "Monstre personnalisé créé depuis le catalogue");
        Check(encounter.GetComponentInChildren<SpriteRenderer>().sprite == GameSession.WorldMap.toolbox.Find("DroneCuivre").sprite, "Sprite du catalogue utilisé en exploration");
        while (Time.timeSinceLevelLoad < .5f) yield return null;
        ScreenCapture.CaptureScreenshot("Logs/atelier-exploration.png");
        GameSession.Leader.Hp = 27;
        var portal = world.Level.Entities.First(e => e.__identifier == "Portal");
        Move(world.Level.Position(portal)); yield return null; Call(world, "Update"); Call(world, "Interact");
        Check(SceneTransition.IsBusy, "Interaction de porte déclenche un fondu");
        while (SceneTransition.IsBusy) yield return null;
        world = Object.FindAnyObjectByType<SandboxWorld>();
        Check(world.Level.identifier != "Laboratoire" && GameSession.Leader.Hp == 27, "Autre carte chargée avec les PV conservés");
        Check(Vector3.Distance(Object.FindAnyObjectByType<PlayerMovement>().transform.position, world.Level.Position(world.Level.Entities.Single(e => e.__identifier == "PlayerSpawn"))) < .1f, "Arrivée au départ de la carte cible");
        portal = world.Level.Entities.First(e => e.__identifier == "Portal");
        Move(world.Level.Position(portal)); yield return null; Call(world, "Update"); Call(world, "Interact");
        while (SceneTransition.IsBusy) yield return null;
        world = Object.FindAnyObjectByType<SandboxWorld>(); Check(world.Level.identifier == "Laboratoire", "Passage retour vers le laboratoire");
        while (Time.timeSinceLevelLoad < 1.2f) yield return null;
        encounter = Object.FindAnyObjectByType<EncounterTrigger>();
        var customId = world.Level.EncounterId(world.Level.Entities.First(e => e.__identifier == "DroneCuivre"));
        Move(encounter.transform.position);
        while (SceneManager.GetActiveScene().name != "SandboxCombat" || SceneTransition.IsBusy) yield return null;
        Check(GameSession.PendingEncounterId == customId, "Contact du monstre personnalisé lance le combat");
        var controller = Object.FindAnyObjectByType<BattleController>();
        Check(Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Any(s => s.name == "Drone cuivre" && s.sprite == GameSession.PendingEnemy.battleSprite), "Sprite personnalisé utilisé en combat");
        while (!Field<Battle>(controller, "battle").IsOver || Field<bool>(controller, "busy"))
        {
            if (!Field<bool>(controller, "busy")) Command(controller, "Attack"); yield return null;
        }
        Check(Field<Battle>(controller, "battle").Winner == Team.Player, "Victoire contre le monstre du catalogue");
        Command(controller, "Return");
        while (SceneManager.GetActiveScene().name != "Atelier" || SceneTransition.IsBusy) yield return null;
        Check(Object.FindAnyObjectByType<SandboxWorld>().Level.identifier == "Laboratoire", "Retour de combat sur le bon niveau LDtk");
        Check(GameSession.IsDefeated(customId) && Object.FindAnyObjectByType<EncounterTrigger>() == null, "Monstre vaincu absent au retour");
        world = Object.FindAnyObjectByType<SandboxWorld>(); portal = world.Level.Entities.First(e => e.__identifier == "Portal");
        Move(world.Level.Position(portal)); yield return null; Call(world, "Update"); Call(world, "Interact");
        while (SceneTransition.IsBusy) yield return null;
        world = Object.FindAnyObjectByType<SandboxWorld>(); portal = world.Level.Entities.First(e => e.__identifier == "Portal");
        Move(world.Level.Position(portal)); yield return null; Call(world, "Update"); Call(world, "Interact");
        while (SceneTransition.IsBusy) yield return null;
        Check(Object.FindAnyObjectByType<EncounterTrigger>() == null && GameSession.IsDefeated(customId), "Progression conservée après aller-retour entre cartes");
    }
}
