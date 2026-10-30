using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu de jeu façon JRPG : Échap / Tab (ou Start à la manette) pour l'ouvrir et le fermer.
/// Met le jeu en pause et affiche le personnage : portrait, PV, caractéristiques,
/// ainsi que le lieu, l'heure et le temps de jeu.
///
/// Il se crée tout seul au lancement (aucune étape dans l'éditeur) et ne s'ouvre que dans
/// une scène où le joueur est présent (pas pendant un combat).
/// L'interface est construite en code avec uGUI, sur une grille de 320 x 180 pixels
/// agrandie d'un facteur entier pour rester nette.
/// </summary>
public class GameMenu : MonoBehaviour
{
    private const float RefWidth = 320f;
    private const float RefHeight = 180f;
    private const float HpBarWidth = 132f;
    private const float FadeDuration = 0.12f;

    private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.98f);
    private static readonly Color DimTextColor = new Color(0.56f, 0.62f, 0.78f);
    private static readonly Color GoldColor = new Color(1f, 0.82f, 0.45f);
    private static readonly Color LineColor = new Color(0.62f, 0.7f, 0.88f, 0.35f);

    private enum Command { Status, Resume }
    private static readonly string[] CommandLabels = { "Statut", "Reprendre" };

    private static GameMenu instance;

    /// <summary>Vrai quand le menu est affiché (le jeu est alors en pause).</summary>
    public static bool IsOpen => instance != null && instance.isOpen;

    private InputAction menuAction, cancelAction, confirmAction, upAction, downAction;

    private Canvas canvas;
    private CanvasScaler scaler;
    private CanvasGroup group;
    private Font font;
    private Sprite panelSprite, cursorSprite, pixelSprite;

    private RectTransform cursor;
    private Text[] commandTexts;
    private Image portrait;
    private Text nameText, hpValueText, attackText, defenseText, speedText;
    private Text locationText, clockText, playTimeText;
    private Image hpFill;

    private bool isOpen;
    private int selected;
    private float previousTimeScale = 1f;
    private float openedAt;

    // ------------------------------------------------------------------ création automatique

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("Game Menu");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameMenu>();
    }

    private void Awake()
    {
        CreateInput();
        font = Resources.Load<Font>("Fonts/PixelifySans");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateSprites();
        BuildUi();
        SetVisible(false);
    }

    private void OnEnable()
    {
        foreach (var a in AllActions()) a.Enable();
    }

    private void OnDisable()
    {
        foreach (var a in AllActions()) a.Disable();
        if (isOpen) Time.timeScale = previousTimeScale;
    }

    private void OnDestroy()
    {
        foreach (var a in AllActions()) a.Dispose();
    }

    private InputAction[] AllActions() => new[] { menuAction, cancelAction, confirmAction, upAction, downAction };

    // ------------------------------------------------------------------ boucle

    private void Update()
    {
        // Agrandissement entier : 1 pixel du menu = N pixels écran, jamais de flou
        scaler.scaleFactor = Mathf.Max(1f, Mathf.Floor(Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight)));

        if (!isOpen)
        {
            if (menuAction.WasPressedThisFrame() && CanOpen()) Open();
            return;
        }

        if (menuAction.WasPressedThisFrame() || cancelAction.WasPressedThisFrame())
        {
            Close();
            return;
        }
        if (upAction.WasPressedThisFrame()) Select(selected - 1);
        if (downAction.WasPressedThisFrame()) Select(selected + 1);
        if (confirmAction.WasPressedThisFrame() && (Command)selected == Command.Resume)
        {
            Close();
            return;
        }

        // Animations en temps réel (le jeu, lui, est en pause)
        group.alpha = Mathf.Clamp01((Time.unscaledTime - openedAt) / FadeDuration);
        var bob = Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f ? 0f : 1f;
        cursor.anchoredPosition = new Vector2(12f + bob, -(16f + selected * 16f));
        RefreshClock();
    }

    private static bool CanOpen() => !SceneTransition.IsBusy && !GameSession.HasPendingEncounter && FindAnyObjectByType<PlayerMovement>() != null;

    private void Open()
    {
        isOpen = true;
        openedAt = Time.unscaledTime;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Select(0);
        RefreshContent();
        SetVisible(true);
    }

    private void Close()
    {
        isOpen = false;
        Time.timeScale = previousTimeScale;
        SetVisible(false);
    }

    private void Select(int index)
    {
        selected = (index + CommandLabels.Length) % CommandLabels.Length;
        for (var i = 0; i < commandTexts.Length; i++)
            commandTexts[i].color = i == selected ? TextColor : DimTextColor;
    }

    private void SetVisible(bool visible)
    {
        canvas.enabled = visible;
        group.alpha = 0f; // fondu d'ouverture géré dans Update
    }

    // ------------------------------------------------------------------ contenu

    private void RefreshContent()
    {
        var member = GameSession.Leader;
        nameText.text = member.Name;
        hpValueText.text = $"{member.Hp} / {member.MaxHp}";
        attackText.text = member.Attack.ToString();
        defenseText.text = member.Defense.ToString();
        speedText.text = member.Speed.ToString();

        var ratio = member.MaxHp > 0 ? (float)member.Hp / member.MaxHp : 0f;
        hpFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, Mathf.Round(HpBarWidth * ratio)), 6f);
        hpFill.color = ratio > 0.5f ? new Color(0.45f, 0.95f, 0.55f)
            : ratio > 0.25f ? new Color(1f, 0.85f, 0.35f)
            : new Color(1f, 0.38f, 0.38f);

        var player = FindAnyObjectByType<PlayerMovement>();
        var animator = player != null ? player.GetComponent<CharacterAnimator>() : null;
        portrait.sprite = animator != null ? animator.Portrait : null;
        portrait.enabled = portrait.sprite != null;

        locationText.text = SceneManager.GetActiveScene().name;
        RefreshClock();
    }

    private void RefreshClock()
    {
        var hours = DayNightCycle.TimeOfDay * 24f;
        var h = Mathf.FloorToInt(hours);
        var m = Mathf.FloorToInt((hours - h) * 60f);
        clockText.text = $"Heure  {h:00}:{m:00}";

        var t = Mathf.FloorToInt(GameSession.PlayTimeSeconds);
        playTimeText.text = $"Jeu  {t / 3600:00}:{t / 60 % 60:00}:{t % 60:00}";
    }

    // ------------------------------------------------------------------ construction de l'interface

    private void BuildUi()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.pixelPerfect = true;
        scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;
        group = canvasGo.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Voile sombre sur tout l'écran
        var dim = NewImage(canvasGo.transform, "Dim", pixelSprite, new Color(0.02f, 0.03f, 0.08f, 0.6f));
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = Vector2.zero;
        dim.rectTransform.offsetMax = Vector2.zero;

        // Zone de 320 x 180 centrée : toutes les positions ci-dessous sont en pixels depuis son coin haut-gauche
        var frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(canvasGo.transform, false);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
        frame.sizeDelta = new Vector2(RefWidth, RefHeight);

        // Commandes
        Panel(frame, 8, 8, 88, 48);
        commandTexts = new Text[CommandLabels.Length];
        for (var i = 0; i < CommandLabels.Length; i++)
            commandTexts[i] = Label(frame, CommandLabels[i], 24, 13 + i * 16, 68, 12, DimTextColor);
        cursor = Place(NewImage(frame, "Cursor", cursorSprite, GoldColor).rectTransform, 12, 16, 8, 8);

        // Infos de partie
        Panel(frame, 8, 124, 88, 48);
        locationText = Label(frame, "", 16, 129, 76, 12, GoldColor);
        clockText = Label(frame, "", 16, 143, 76, 12, TextColor);
        playTimeText = Label(frame, "", 16, 157, 76, 12, TextColor);

        // Fiche du personnage
        Panel(frame, 104, 8, 208, 164);
        Panel(frame, 114, 18, 44, 56);
        portrait = Place(NewImage(frame, "Portrait", null, Color.white).rectTransform, 120, 22, 32, 48).GetComponent<Image>();
        portrait.preserveAspect = true;

        nameText = Label(frame, "", 166, 16, 140, 20, GoldColor);
        Label(frame, "PV", 166, 44, 40, 12, DimTextColor);
        hpValueText = Label(frame, "", 166, 44, 134, 12, TextColor, TextAnchor.UpperRight);
        Place(NewImage(frame, "HP Back", pixelSprite, new Color(0.03f, 0.04f, 0.09f)).rectTransform, 166, 60, HpBarWidth + 2, 8);
        hpFill = NewImage(frame, "HP Fill", pixelSprite, Color.green);
        Place(hpFill.rectTransform, 167, 61, HpBarWidth, 6);

        Place(NewImage(frame, "Line", pixelSprite, LineColor).rectTransform, 114, 84, 188, 1);
        Label(frame, "Caractéristiques", 114, 90, 188, 12, GoldColor);
        attackText = StatRow(frame, "Attaque", 108);
        defenseText = StatRow(frame, "Défense", 124);
        speedText = StatRow(frame, "Vitesse", 140);

        Label(frame, "Échap : fermer", 114, 157, 188, 12, DimTextColor, TextAnchor.UpperRight);
    }

    private Text StatRow(RectTransform parent, string label, float y)
    {
        Label(parent, label, 122, y, 120, 12, TextColor);
        return Label(parent, "", 122, y, 178, 12, TextColor, TextAnchor.UpperRight);
    }

    private void Panel(RectTransform parent, float x, float y, float w, float h)
    {
        var img = NewImage(parent, "Panel", panelSprite, Color.white);
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, x, y, w, h);
    }

    private Text Label(RectTransform parent, string text, float x, float y, float w, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        Place(t.rectTransform, x, y, w, size + 4);
        return t;
    }

    private static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Positionne un élément en pixels depuis le coin haut-gauche de son parent.</summary>
    private static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    // ------------------------------------------------------------------ sprites dessinés en code

    private void CreateSprites()
    {
        // Cadre de fenêtre 16 x 16, découpé en 9 (bords de 4 px) pour s'étirer sans se déformer
        var frameTex = NewTexture(16, 16);
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
        {
            var edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(15 - x, 15 - y));
            var corner = (x == 0 || x == 15) && (y == 0 || y == 15);
            Color c;
            if (corner) c = Color.clear;
            else if (edge == 0) c = new Color(0.04f, 0.05f, 0.10f, 1f);
            else if (edge == 1) c = new Color(0.62f, 0.70f, 0.88f, 1f);
            else if (edge == 2) c = new Color(0.10f, 0.12f, 0.24f, 0.97f);
            else c = Color.Lerp(new Color(0.07f, 0.09f, 0.20f, 0.95f), new Color(0.13f, 0.17f, 0.34f, 0.95f), y / 15f);
            frameTex.SetPixel(x, y, c);
        }
        frameTex.Apply();
        panelSprite = Sprite.Create(frameTex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));

        // Curseur : petite flèche vers la droite
        var cursorTex = NewTexture(8, 8);
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var inside = y >= 1 && y <= 7 && x <= 3 - Mathf.Abs(y - 4) + 1;
            cursorTex.SetPixel(x, y, inside ? Color.white : Color.clear);
        }
        cursorTex.Apply();
        cursorSprite = Sprite.Create(cursorTex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f);

        // Pixel blanc (barres, voile, lignes), teinté par la couleur de l'Image
        var pixelTex = NewTexture(1, 1);
        pixelTex.SetPixel(0, 0, Color.white);
        pixelTex.Apply();
        pixelSprite = Sprite.Create(pixelTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Texture2D NewTexture(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

    // ------------------------------------------------------------------ entrées

    private void CreateInput()
    {
        menuAction = new InputAction("Menu", InputActionType.Button);
        menuAction.AddBinding("<Keyboard>/escape");
        menuAction.AddBinding("<Keyboard>/tab");
        menuAction.AddBinding("<Gamepad>/start");

        cancelAction = new InputAction("Cancel", InputActionType.Button);
        cancelAction.AddBinding("<Keyboard>/backspace");
        cancelAction.AddBinding("<Gamepad>/buttonEast");

        confirmAction = new InputAction("Confirm", InputActionType.Button);
        confirmAction.AddBinding("<Keyboard>/enter");
        confirmAction.AddBinding("<Keyboard>/numpadEnter");
        confirmAction.AddBinding("<Keyboard>/space");
        confirmAction.AddBinding("<Gamepad>/buttonSouth");

        // Positions physiques : "w"/"s" = Z/S sur un clavier AZERTY
        upAction = new InputAction("Up", InputActionType.Button);
        upAction.AddBinding("<Keyboard>/upArrow");
        upAction.AddBinding("<Keyboard>/w");
        upAction.AddBinding("<Gamepad>/dpad/up");

        downAction = new InputAction("Down", InputActionType.Button);
        downAction.AddBinding("<Keyboard>/downArrow");
        downAction.AddBinding("<Keyboard>/s");
        downAction.AddBinding("<Gamepad>/dpad/down");
    }
}
