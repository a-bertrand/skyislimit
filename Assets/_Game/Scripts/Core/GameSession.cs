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
    private static Vector3? playerReturnPosition;

    public static string PendingEncounterId { get; private set; }
    public static CombatantDefinition PendingEnemy { get; private set; }
    public static string ReturnSceneName { get; private set; }
    public static bool HasPendingEncounter => !string.IsNullOrEmpty(PendingEncounterId);

    // Le projet a "Enter Play Mode Options" activé : les variables statiques ne sont PAS
    // remises à zéro entre deux Play dans l'éditeur. On le fait nous-mêmes à chaque lancement.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        defeatedEncounters.Clear();
        playerReturnPosition = null;
        ClearPending();
    }

    public static bool IsDefeated(string encounterId) => defeatedEncounters.Contains(encounterId);

    /// <summary>Appelé par un ennemi de la carte : mémorise d'où on vient et charge la scène de combat.</summary>
    public static void StartEncounter(string encounterId, CombatantDefinition enemy, Vector3 playerPosition)
    {
        PendingEncounterId = encounterId;
        PendingEnemy = enemy;
        ReturnSceneName = SceneManager.GetActiveScene().name;
        playerReturnPosition = playerPosition;
        SceneManager.LoadScene(CombatSceneName);
    }

    /// <summary>Appelé à la fin du combat : enregistre le résultat et retourne sur la carte.</summary>
    public static void EndEncounter(bool victory)
    {
        if (!HasPendingEncounter) return;

        if (victory) defeatedEncounters.Add(PendingEncounterId);
        var returnScene = ReturnSceneName;
        ClearPending();
        SceneManager.LoadScene(returnScene);
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
