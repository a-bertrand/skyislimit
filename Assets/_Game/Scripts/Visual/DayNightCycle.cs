using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cycle jour / nuit piloté par le joueur (clin d'œil à Sea of Stars) :
/// maintenir T fait défiler le temps. Fait varier la lumière globale 2D,
/// et expose NightFactor pour que les lampes brillent plus fort la nuit.
/// À poser sur la Global Light 2D.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class DayNightCycle : MonoBehaviour
{
    /// <summary>0 = plein jour, 1 = pleine nuit. Lu par les lumières (LightFlicker).</summary>
    public static float NightFactor { get; private set; } = 1f;

    /// <summary>Heure courante : 0 = minuit, 0.5 = midi. Lue par le menu.</summary>
    public static float TimeOfDay { get; private set; }

    [Tooltip("Heure de départ : 0 = minuit, 0.25 = aube, 0.5 = midi, 0.75 = crépuscule.")]
    [Range(0f, 1f)] [SerializeField] private float timeOfDay = 0.95f;
    [Tooltip("Vitesse quand on maintient la touche (fraction de journée par seconde).")]
    [SerializeField] private float holdSpeed = 0.2f;
    [SerializeField] private bool showHint = true;

    // Couleurs et intensités aux 4 moments clés de la journée
    private static readonly Color Midnight = new Color(0.30f, 0.36f, 0.70f);
    private static readonly Color Dawn = new Color(1.00f, 0.62f, 0.55f);
    private static readonly Color Noon = new Color(1.00f, 0.97f, 0.90f);
    private static readonly Color Dusk = new Color(1.00f, 0.52f, 0.40f);
    private const float NightIntensity = 0.35f;
    private const float DayIntensity = 1.05f;

    private Light2D globalLight;

    public void Setup(float startTime, bool hint)
    {
        timeOfDay = startTime;
        showHint = hint;
        Apply();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        NightFactor = 1f;
        TimeOfDay = 0f;
    }

    private void Awake()
    {
        globalLight = GetComponent<Light2D>();
        Apply();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tKey.isPressed)
            timeOfDay = Mathf.Repeat(timeOfDay + holdSpeed * Time.deltaTime, 1f);

        Apply();
    }

    private void Apply()
    {
        // Soleil : -1 à minuit, +1 à midi
        var sun = -Mathf.Cos(timeOfDay * Mathf.PI * 2f);
        var day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.35f, 0.45f, sun));

        Color color;
        if (timeOfDay < 0.25f) color = Color.Lerp(Midnight, Dawn, Mathf.SmoothStep(0f, 1f, timeOfDay / 0.25f));
        else if (timeOfDay < 0.5f) color = Color.Lerp(Dawn, Noon, Mathf.SmoothStep(0f, 1f, (timeOfDay - 0.25f) / 0.25f));
        else if (timeOfDay < 0.75f) color = Color.Lerp(Noon, Dusk, Mathf.SmoothStep(0f, 1f, (timeOfDay - 0.5f) / 0.25f));
        else color = Color.Lerp(Dusk, Midnight, Mathf.SmoothStep(0f, 1f, (timeOfDay - 0.75f) / 0.25f));

        globalLight.color = color;
        globalLight.intensity = Mathf.Lerp(NightIntensity, DayIntensity, day);
        NightFactor = 1f - day;
        TimeOfDay = timeOfDay;
    }

    private void OnGUI()
    {
        if (!showHint || GameMenu.IsOpen) return;
        var hours = Mathf.FloorToInt(timeOfDay * 24f);
        var minutes = Mathf.FloorToInt((timeOfDay * 24f - hours) * 60f);
        GUI.Label(new Rect(10, Screen.height - 28, 400, 24), $"{hours:00}:{minutes:00}  —  maintenir T : faire avancer le temps");
    }
}
