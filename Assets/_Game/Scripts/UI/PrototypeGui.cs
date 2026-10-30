using UnityEngine;

/// <summary>Habillage léger partagé par le HUD et le combat ; coordonnées logiques 960 × 540.</summary>
public static class PrototypeGui
{
    public static readonly Color Ink = new Color(0.035f, 0.055f, 0.12f, 0.96f);
    public static readonly Color Cyan = new Color(0.40f, 0.92f, 0.91f);
    public static readonly Color Muted = new Color(0.63f, 0.69f, 0.82f);
    public static readonly Color Gold = new Color(1f, 0.77f, 0.40f);
    private static Font font;
    private static Matrix4x4 previousMatrix;

    public static void Begin()
    {
        if (font == null) font = Resources.Load<Font>("Fonts/PixelifySans") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        previousMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 960f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 960 * scale) / 2, (Screen.height - 540 * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
    }
    public static void End() { GUI.matrix = previousMatrix; GUI.color = Color.white; }
    public static void Fill(Rect rect, Color color)
    {
        var saved = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = saved;
    }
    public static void Panel(Rect rect)
    {
        Fill(rect, new Color(0.24f, 0.36f, 0.50f));
        Fill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), Ink);
    }
    public static void Text(Rect rect, string text, int size = 20, Color? color = null)
    {
        var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, wordWrap = true };
        style.normal.textColor = color ?? Color.white;
        GUI.Label(rect, text, style);
    }
    public static bool Button(Rect rect, string label, bool enabled = true)
    {
        bool saved = GUI.enabled; GUI.enabled = enabled;
        var style = new GUIStyle(GUI.skin.button) { font = font, fontSize = 22 };
        style.normal.textColor = Cyan;
        bool clicked = GUI.Button(rect, label, style);
        GUI.enabled = saved;
        return clicked;
    }
    public static void Hp(Rect rect, int hp, int max)
    {
        Fill(rect, new Color(0.13f, 0.17f, 0.27f));
        Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01((float)hp / max), rect.height), hp > max * 0.3f ? Cyan : Gold);
    }
}
