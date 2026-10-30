using UnityEngine;

/// <summary>Le cadrage de combat reste en 16:9, y compris dans une fenêtre Game verticale.</summary>
[RequireComponent(typeof(Camera))]
public sealed class PrototypeViewport : MonoBehaviour
{
    private Camera target;
    private void Awake() { target = GetComponent<Camera>(); Update(); }
    private void Update()
    {
        float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        const float aspect = 16f / 9f;
        if (screenAspect > aspect)
        {
            float width = aspect / screenAspect;
            target.rect = new Rect((1 - width) / 2, 0, width, 1);
        }
        else
        {
            float height = screenAspect / aspect;
            target.rect = new Rect(0, (1 - height) / 2, 1, height);
        }
        target.aspect = aspect;
    }
}
