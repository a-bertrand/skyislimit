using UnityEngine;

/// <summary>
/// Ennemi visible sur la carte : quand le joueur le touche, le combat se lance.
/// Une fois vaincu, il ne réapparaît plus (pendant la session de jeu).
/// À poser sur un sprite de la scène d'exploration ; le collider est ajouté et réglé en code.
/// </summary>
public class EncounterTrigger : MonoBehaviour
{
    [Tooltip("Fiche de l'ennemi. Vide = ennemi par défaut du BattleController.")]
    [SerializeField] private CombatantDefinition enemy;

    [Tooltip("Identifiant unique de la rencontre. Vide = nom de la scène + nom de l'objet.")]
    [SerializeField] private string encounterId;

    [Tooltip("Secondes après l'arrivée sur la carte pendant lesquelles la rencontre ne se déclenche pas.")]
    [SerializeField] private float armDelay = 1f;

    private bool triggered;

    private string Id => string.IsNullOrEmpty(encounterId) ? $"{gameObject.scene.name}/{gameObject.name}" : encounterId;

    private void Awake()
    {
        if (GameSession.IsDefeated(Id))
        {
            gameObject.SetActive(false);
            return;
        }

        // Zone de contact : un collider en mode "trigger" détecte le joueur sans le bloquer
        var col = GetComponent<Collider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Délai de grâce : évite de relancer le combat si on revient sur la carte collé à l'ennemi
        if (triggered || Time.timeSinceLevelLoad < armDelay) return;

        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        triggered = true;
        GameSession.StartEncounter(Id, enemy, player.transform.position);
    }
}
