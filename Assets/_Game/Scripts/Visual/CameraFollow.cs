using UnityEngine;

/// <summary>
/// La caméra suit une cible en douceur, sans sortir des limites de la carte.
/// À poser sur la Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.15f;
    [Tooltip("Décalage vertical : on montre un peu plus ce qu'il y a devant le perso.")]
    [SerializeField] private float verticalOffset = 0.5f;
    [Tooltip("Limites de la carte (en unités). Largeur/hauteur à 0 = pas de limite.")]
    [SerializeField] private Rect bounds;
    [Tooltip("Aligne la caméra sur la grille de pixels (16 px par unité) pour éviter le scintillement.")]
    [SerializeField] private bool snapToPixelGrid = true;
    [SerializeField] private int pixelsPerUnit = 16;

    private Camera cam;
    private Vector3 velocity;
    private Vector3 smoothedPosition;

    /// <summary>Utilisé par l'outil qui construit la scène.</summary>
    public void Setup(Transform followTarget, Rect mapBounds)
    {
        target = followTarget;
        bounds = mapBounds;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (target == null) return;
        smoothedPosition = Desired();
        Apply(smoothedPosition);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        smoothedPosition = Vector3.SmoothDamp(smoothedPosition, Desired(), ref velocity, smoothTime);
        Apply(smoothedPosition);
    }

    private Vector3 Desired()
    {
        var p = target.position + new Vector3(0f, verticalOffset, 0f);
        p.z = transform.position.z;

        if (bounds.width > 0f && bounds.height > 0f)
        {
            var halfH = cam.orthographicSize;
            var halfW = halfH * cam.aspect;
            p.x = bounds.width > halfW * 2f ? Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW) : bounds.center.x;
            p.y = bounds.height > halfH * 2f ? Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH) : bounds.center.y;
        }
        return p;
    }

    private void Apply(Vector3 p)
    {
        if (snapToPixelGrid && pixelsPerUnit > 0)
        {
            p.x = Mathf.Round(p.x * pixelsPerUnit) / pixelsPerUnit;
            p.y = Mathf.Round(p.y * pixelsPerUnit) / pixelsPerUnit;
        }
        transform.position = p;
    }
}
