using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Fondu commun aux rencontres et au retour sur la carte, avec verrou d'entrée.</summary>
public sealed class SceneTransition : MonoBehaviour
{
    private static SceneTransition instance;
    private float alpha;
    public static bool IsBusy => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static void Load(string scene)
    {
        if (IsBusy) return;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("Scène absente du build : " + scene + ". Lance Tools > RPG > Bac à sable > Préparer / ouvrir.");
            return;
        }
        instance = new GameObject("Scene transition").AddComponent<SceneTransition>();
        DontDestroyOnLoad(instance.gameObject);
        instance.StartCoroutine(instance.Transition(scene));
    }

    private IEnumerator Transition(string scene)
    {
        Time.timeScale = 1f;
        for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime) { alpha = t / 0.18f; yield return null; }
        alpha = 1;
        yield return SceneManager.LoadSceneAsync(scene);
        for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime) { alpha = 1 - t / 0.22f; yield return null; }
        instance = null;
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        var previous = GUI.color;
        GUI.depth = -10000;
        GUI.color = new Color(0.02f, 0.03f, 0.08f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
