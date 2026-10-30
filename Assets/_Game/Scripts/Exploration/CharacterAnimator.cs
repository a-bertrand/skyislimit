using UnityEngine;

/// <summary>
/// Anime un perso vu de dessus selon sa vitesse : 3 directions (bas, haut, côté)
/// x 3 images (0 = immobile, 1 et 2 = pas de marche). Le côté gauche = côté droit retourné.
/// Un deuxième SpriteRenderer optionnel affiche les parties lumineuses (visière...).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CharacterAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer glow;
    [SerializeField] private Sprite[] down = new Sprite[3];
    [SerializeField] private Sprite[] up = new Sprite[3];
    [SerializeField] private Sprite[] side = new Sprite[3];
    [SerializeField] private Sprite[] downGlow = new Sprite[3];
    [SerializeField] private Sprite[] upGlow = new Sprite[3];
    [SerializeField] private Sprite[] sideGlow = new Sprite[3];
    [SerializeField] private float framesPerSecond = 8f;

    private static readonly int[] WalkCycle = { 1, 0, 2, 0 };

    private enum Facing { Down, Up, Left, Right }

    private Rigidbody2D rb;
    private Facing facing = Facing.Down;

    /// <summary>Image de face, immobile : utilisée comme portrait dans le menu.</summary>
    public Sprite Portrait => down != null && down.Length > 0 ? down[0] : null;
    private float walkTime;

    /// <summary>Utilisé par l'outil qui construit la scène.</summary>
    public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer,
        Sprite[] downFrames, Sprite[] upFrames, Sprite[] sideFrames,
        Sprite[] downGlowFrames, Sprite[] upGlowFrames, Sprite[] sideGlowFrames)
    {
        body = bodyRenderer;
        glow = glowRenderer;
        down = downFrames; up = upFrames; side = sideFrames;
        downGlow = downGlowFrames; upGlow = upGlowFrames; sideGlow = sideGlowFrames;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        var velocity = rb.linearVelocity;
        var moving = velocity.sqrMagnitude > 0.01f;

        if (moving)
        {
            if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y))
                facing = velocity.x > 0f ? Facing.Right : Facing.Left;
            else
                facing = velocity.y > 0f ? Facing.Up : Facing.Down;
            walkTime += Time.deltaTime;
        }
        else
        {
            walkTime = 0f;
        }

        var frame = moving ? WalkCycle[(int)(walkTime * framesPerSecond) % WalkCycle.Length] : 0;
        Sprite[] frames, glowFrames;
        switch (facing)
        {
            case Facing.Up: frames = up; glowFrames = upGlow; break;
            case Facing.Left:
            case Facing.Right: frames = side; glowFrames = sideGlow; break;
            default: frames = down; glowFrames = downGlow; break;
        }

        var flip = facing == Facing.Left;
        if (body != null && frames != null && frame < frames.Length)
        {
            body.sprite = frames[frame];
            body.flipX = flip;
        }
        if (glow != null && glowFrames != null && frame < glowFrames.Length)
        {
            glow.sprite = glowFrames[frame];
            glow.flipX = flip;
        }
    }
}
