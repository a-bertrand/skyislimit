using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Déplacement top-down (vue du dessus, façon JRPG SNES).
/// Clavier : ZQSD (AZERTY) / WASD (QWERTY) / flèches. Manette : stick gauche.
/// À poser sur un GameObject qui a un Rigidbody2D (ajouté automatiquement sinon).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f; // unités Unity par seconde

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;

    private void Awake()
    {
        // Config physique faite en code plutôt qu'à la main dans l'Inspector
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;                              // pas de gravité en vue du dessus
        rb.freezeRotation = true;                          // le perso ne tourne pas sur lui-même
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // mouvement fluide

        // Action "Move" définie en code (Input System).
        // Les bindings clavier sont par position physique : "w" = la touche Z sur un AZERTY.
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    private void Start()
    {
        // Retour de combat : on replace le joueur là où il était
        if (GameSession.TryConsumePlayerReturnPosition(out var position))
        {
            transform.position = position;
            rb.position = position;
        }
    }

    private void OnEnable() => moveAction.Enable();
    private void OnDisable() => moveAction.Disable();

    private void Update()
    {
        // Lecture des entrées à chaque frame
        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
    }

    private void FixedUpdate()
    {
        // Application du mouvement au pas de la physique (gère les collisions)
        rb.linearVelocity = moveInput * moveSpeed;
    }
}