using UnityEngine;

/// <summary>
/// Patrouille simple d'un monstre sur la carte : allers-retours haut ↔ bas
/// autour de sa position de départ, avec une petite pause à chaque bout.
/// À poser sur le même GameObject que l'EncounterTrigger ; le Rigidbody2D est réglé en code.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MobMovement : MonoBehaviour
{
    [Tooltip("Distance parcourue au-dessus et en dessous de la position de départ (unités Unity).")]
    [SerializeField] private float range = 2f;

    [SerializeField] private float moveSpeed = 2f; // unités Unity par seconde

    [Tooltip("Secondes d'arrêt à chaque extrémité du trajet.")]
    [SerializeField] private float pauseAtEnds = 0.5f;

    private Rigidbody2D rb;
    private Vector2 origin;
    private int direction = 1; // 1 = monte, -1 = descend
    private float pauseTimer;

    private void Awake()
    {
        // Kinematic : déplacé par le code, ni gravité ni poussée du joueur.
        // Le trigger de l'EncounterTrigger détecte toujours le contact avec le joueur.
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // mouvement fluide
        origin = transform.position;
    }

    private void FixedUpdate()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            return;
        }

        var target = origin + Vector2.up * (range * direction);
        var next = Vector2.MoveTowards(rb.position, target, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        // Bout du trajet atteint : demi-tour après la pause
        if (next == target)
        {
            direction = -direction;
            pauseTimer = pauseAtEnds;
        }
    }

    // Affiche le trajet dans la vue Scene quand l'objet est sélectionné
    private void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? origin : (Vector2)transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center + Vector2.up * range, center + Vector2.down * range);
    }
}
