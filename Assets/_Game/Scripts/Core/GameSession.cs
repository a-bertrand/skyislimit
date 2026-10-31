using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// État de la partie en cours, partagé entre les scènes (exploration ↔ combat).
/// Les données statiques survivent aux changements de scène.
/// Plus tard, c'est ce contenu qu'on écrira dans la sauvegarde (avec les flags narratifs).
/// </summary>
public static class GameSession
{
    public const string CombatSceneName = "Combat";

    private static readonly HashSet<string> defeatedEncounters = new HashSet<string>();
    private static readonly List<PartyMember> party = new List<PartyMember>();
    private static Vector3? playerReturnPosition;

    public static string PendingEncounterId { get; private set; }
    public static CombatantDefinition PendingEnemy { get; private set; }
    public static string ReturnSceneName { get; private set; }
    public static bool HasPendingEncounter => !string.IsNullOrEmpty(PendingEncounterId);
    public static LdtkMap WorldMap { get; set; }
    public static string WorldLevel { get; set; }
    private static readonly HashSet<string> activatedBeacons = new HashSet<string>();
    public static bool BeaconActivated
    {
        get => activatedBeacons.Contains(WorldLevel ?? "");
        set { if (value) activatedBeacons.Add(WorldLevel ?? ""); else activatedBeacons.Remove(WorldLevel ?? ""); }
    }
    public static void Travel(string destination)
    {
        if (HasPendingEncounter || SceneTransition.IsBusy || WorldMap == null) return;
        if (!System.Array.Exists(WorldMap.Read().levels, l => l.identifier == destination)) return;
        WorldLevel = destination; playerReturnPosition = null;
        SceneTransition.Load(SceneManager.GetActiveScene().name);
    }

    /// <summary>L'équipe du joueur (un seul membre pour l'instant). Créée à la première lecture.</summary>
    public static IReadOnlyList<PartyMember> Party
    {
        get
        {
            if (party.Count == 0) party.Add(new PartyMember("Héros", 40, 12, 6, 10));
            return party;
        }
    }

    public static PartyMember Leader => Party[0];

    /// <summary>Temps de jeu en secondes (pauses comprises, comme dans les JRPG).</summary>
    public static float PlayTimeSeconds => Time.unscaledTime;

    // Le projet a "Enter Play Mode Options" activé : les variables statiques ne sont PAS
    // remises à zéro entre deux Play dans l'éditeur. On le fait nous-mêmes à chaque lancement.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        defeatedEncounters.Clear();
        party.Clear();
        activatedBeacons.Clear(); WorldMap = null; WorldLevel = null;
        playerReturnPosition = null;
        BeaconActivated = false;
        Time.timeScale = 1f;
        ClearPending();
    }

    public static bool IsDefeated(string encounterId) => defeatedEncounters.Contains(encounterId);

    /// <summary>Appelé par un ennemi de la carte : mémorise d'où on vient et charge la scène de combat.</summary>
    public static bool StartEncounter(string encounterId, CombatantDefinition enemy, Vector3 playerPosition, string combatScene = CombatSceneName)
    {
        if (HasPendingEncounter || SceneTransition.IsBusy || IsDefeated(encounterId)) return false;
        if (!Application.CanStreamedLevelBeLoaded(combatScene))
        {
            Debug.LogError("Scène de combat absente du build : " + combatScene);
            return false;
        }
        PendingEncounterId = encounterId;
        PendingEnemy = enemy;
        ReturnSceneName = SceneManager.GetActiveScene().name;
        playerReturnPosition = playerPosition;
        SceneTransition.Load(combatScene);
        return true;
    }

    /// <summary>Appelé à la fin du combat : enregistre le résultat et retourne sur la carte.</summary>
    public static void EndEncounter(bool victory)
    {
        if (!HasPendingEncounter) return;

        if (victory) defeatedEncounters.Add(PendingEncounterId);
        else playerReturnPosition = null; // Défaite : retour au point de départ, hors du contact ennemi.
        var returnScene = ReturnSceneName;
        ClearPending();
        SceneTransition.Load(returnScene);
    }

    public static void RestartSandbox()
    {
        if (HasPendingEncounter || SceneTransition.IsBusy) return;
        defeatedEncounters.Clear();
        activatedBeacons.Clear();
        BeaconActivated = false;
        Leader.Hp = Leader.MaxHp;
        playerReturnPosition = null;
        SceneTransition.Load(SceneManager.GetActiveScene().name);
    }

    /// <summary>Position où replacer le joueur en revenant sur la carte (lue une seule fois).</summary>
    public static bool TryConsumePlayerReturnPosition(out Vector3 position)
    {
        position = playerReturnPosition.GetValueOrDefault();
        var has = playerReturnPosition.HasValue;
        playerReturnPosition = null;
        return has;
    }

    private static void ClearPending()
    {
        PendingEncounterId = null;
        PendingEnemy = null;
        ReturnSceneName = null;
    }
}
