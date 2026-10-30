using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fait vivre une lumière 2D : léger vacillement (lampe) ou pulsation (balise, néon).
/// L'intensité augmente la nuit (DayNightCycle.NightFactor).
/// </summary>
[RequireComponent(typeof(Light2D))]
public class LightFlicker : MonoBehaviour
{
    public enum Mode { Steady, Flicker, Pulse }

    [SerializeField] private Mode mode = Mode.Flicker;
    [SerializeField] private float baseIntensity = 1f;
    [Tooltip("Amplitude de la variation (0 = aucune).")]
    [SerializeField] private float amount = 0.15f;
    [SerializeField] private float speed = 6f;
    [Tooltip("Part de l'intensité conservée en plein jour.")]
    [Range(0f, 1f)] [SerializeField] private float dayFactor = 0.2f;

    private Light2D light2D;
    private float seed;

    /// <summary>Utilisé par l'outil qui construit la scène.</summary>
    public void Setup(Mode lightMode, float intensity, float variation, float frequency, float keptByDay)
    {
        mode = lightMode;
        baseIntensity = intensity;
        amount = variation;
        speed = frequency;
        dayFactor = keptByDay;
    }

    private void Awake()
    {
        light2D = GetComponent<Light2D>();
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float variation;
        switch (mode)
        {
            case Mode.Flicker:
                variation = (Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f) * 2f * amount;
                break;
            case Mode.Pulse:
                variation = Mathf.Sin(Time.time * speed + seed) * amount;
                break;
            default:
                variation = 0f;
                break;
        }

        var night = Mathf.Lerp(dayFactor, 1f, DayNightCycle.NightFactor);
        light2D.intensity = Mathf.Max(0f, baseIntensity * (1f + variation) * night);
    }
}
