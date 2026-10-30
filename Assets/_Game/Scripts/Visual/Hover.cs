using UnityEngine;

/// <summary>Fait flotter un objet de haut en bas (drone, objet magique...).</summary>
public class Hover : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.12f;
    [SerializeField] private float speed = 2.5f;

    private Vector3 origin;
    private float phase;

    private void Awake()
    {
        origin = transform.localPosition;
        phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        // Arrondi au pixel (1/16 d'unité) pour garder un rendu pixel art net
        var offset = Mathf.Round(Mathf.Sin(Time.time * speed + phase) * amplitude * 16f) / 16f;
        transform.localPosition = origin + new Vector3(0f, offset, 0f);
    }
}
