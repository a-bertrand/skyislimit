using UnityEngine;

/// <summary>Patrouille horizontale bornée ; les murs et caisses sont détectés avant chaque pas.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class SandboxPatrol : MonoBehaviour
{
    public float radius = 1.5f, speed = 0.8f;
    private Rigidbody2D body;
    private Vector2 origin;
    private float pause;
    private int direction = 1;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[8];

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        origin = transform.position;
    }

    private void FixedUpdate()
    {
        if (SceneTransition.IsBusy || GameSession.HasPendingEncounter || radius <= 0 || speed <= 0) return;
        if (pause > 0) { pause -= Time.fixedDeltaTime; return; }
        Vector2 delta = Vector2.right * direction;
        float step = speed * Time.fixedDeltaTime;
        var filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = false;
        var count = Physics2D.CircleCast(body.position, 0.3f, delta, filter, hits, step + 0.08f);
        bool blocked = false;
        for (int i = 0; i < count; i++)
            if (hits[i].collider.attachedRigidbody != body) blocked = true;
        if (blocked || Mathf.Abs(body.position.x + step * direction - origin.x) > radius)
        { direction = -direction; pause = 0.5f; return; }
        body.MovePosition(body.position + delta * step);
    }
}
