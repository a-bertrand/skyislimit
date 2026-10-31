using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Présentation du moteur pur : commandes, sprites, attaques séquencées et retour sur la carte.</summary>
public class BattleController : MonoBehaviour
{
    [SerializeField] private CombatantDefinition enemy;
    [SerializeField] private SandboxArt art;
    [SerializeField] private float enemyTurnDelay = 0.55f;
    private Battle battle;
    private Combatant heroCombatant;
    private readonly IActionSelector enemyAI = new BasicEnemyAI();
    private readonly List<string> log = new List<string>();
    private readonly Dictionary<Combatant, SpriteRenderer> views = new Dictionary<Combatant, SpriteRenderer>();
    private readonly Dictionary<Combatant, Color> colors = new Dictionary<Combatant, Color>();
    private bool busy;
    private Sprite backgroundSprite, fallbackSprite;
    private SpriteRenderer background;
    private string damageText;
    private Vector3 damagePosition;
    private enum Command { None, Attack, Defend, Return, Restart }
    private Command pending;

    public void Configure(SandboxArt visuals) => art = visuals;
    private bool IsPlayerTurn => battle != null && !battle.IsOver && !busy && !SceneTransition.IsBusy && battle.CurrentActor.Team == Team.Player;
    private void Start()
    {
        if (art != null && art.battleBackground != null)
        {
            if (Camera.main != null && Camera.main.GetComponent<PrototypeViewport>() == null)
                Camera.main.gameObject.AddComponent<PrototypeViewport>();
            var texture = art.battleBackground;
            backgroundSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, texture.height / 11.25f);
            background = new GameObject("Décor de combat").AddComponent<SpriteRenderer>();
            background.transform.SetParent(transform, false);
            background.sprite = backgroundSprite; background.sortingOrder = -200; background.sharedMaterial = art.unlit;
        }
        SetupBattle(false);
    }

    private void Update()
    {
        if (background != null && Camera.main != null)
        {
            float scale = Mathf.Max(1, Camera.main.aspect / ((float)art.battleBackground.width / art.battleBackground.height));
            background.transform.localScale = Vector3.one * scale;
        }
        if (SceneTransition.IsBusy) { pending = Command.None; return; }
        var keyboard = Keyboard.current;
        bool confirm = keyboard?.enterKey.wasPressedThisFrame == true || keyboard?.spaceKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
        bool guard = keyboard?.gKey.wasPressedThisFrame == true || Gamepad.current?.buttonWest.wasPressedThisFrame == true;
        if (!busy)
        {
            if (battle.IsOver && confirm) pending = GameSession.HasPendingEncounter ? Command.Return : Command.Restart;
            else if (IsPlayerTurn && confirm) pending = Command.Attack;
            else if (IsPlayerTurn && guard) pending = Command.Defend;
            if (battle.IsOver && battle.Winner != Team.Player && keyboard?.rKey.wasPressedThisFrame == true) pending = Command.Restart;
        }
        var command = pending; pending = Command.None;
        switch (command)
        {
            case Command.Attack:
                if (IsPlayerTurn) StartCoroutine(Resolve(CombatAction.Attack(battle.CurrentActor, battle.AliveOpponentsOf(battle.CurrentActor).First())));
                break;
            case Command.Defend:
                if (IsPlayerTurn) StartCoroutine(Resolve(CombatAction.Defend(battle.CurrentActor)));
                break;
            case Command.Return:
                if (!busy && battle.IsOver) ReturnToMap();
                break;
            case Command.Restart:
                if (!busy && battle.IsOver) SetupBattle(true);
                break;
        }
    }

    private void SetupBattle(bool restoreHealth)
    {
        StopAllCoroutines();
        foreach (var view in views.Values) Destroy(view.gameObject);
        views.Clear(); colors.Clear(); log.Clear(); damageText = null; pending = Command.None;
        if (restoreHealth) GameSession.Leader.Hp = GameSession.Leader.MaxHp;
        heroCombatant = GameSession.Leader.ToCombatant();
        var definition = GameSession.PendingEnemy != null ? GameSession.PendingEnemy : enemy;
        var opponent = definition != null ? definition.Create(Team.Enemy) : new Combatant("Drone pirate", Team.Enemy, 50, 11, 4, 8);
        battle = new Battle(new[] { heroCombatant, opponent });
        Spawn(heroCombatant, new Vector3(-4, -0.9f, 0), Color.white, true);
        Spawn(opponent, new Vector3(4, -0.5f, 0), definition != null ? definition.placeholderColor : Color.white, false);
        StartCoroutine(Opening());
    }

    private IEnumerator Opening()
    {
        busy = true;
        yield return Present(battle.Start());
        yield return EnemyTurns();
        busy = false;
    }

    private IEnumerator Resolve(CombatAction action)
    {
        busy = true; // Bloque les doubles clics jusqu'à la fin de l'animation ET de la réponse ennemie.
        yield return Present(battle.Execute(action));
        yield return EnemyTurns();
        busy = false;
    }

    private IEnumerator EnemyTurns()
    {
        while (!battle.IsOver && battle.CurrentActor.Team == Team.Enemy)
        {
            yield return new WaitForSeconds(enemyTurnDelay);
            yield return Present(battle.Execute(enemyAI.Choose(battle, battle.CurrentActor)));
        }
    }

    private IEnumerator Present(IReadOnlyList<CombatEvent> events)
    {
        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case CombatEventKind.RoundStarted: AddLog("— Manche " + e.Round + " —"); break;
                case CombatEventKind.Attacked:
                    AddLog(e.Actor.Name + " : " + e.Amount + " dégâts sur " + e.Target.Name);
                    yield return AttackAnimation(e.Actor, e.Target, e.Amount);
                    break;
                case CombatEventKind.Defended:
                    AddLog(e.Actor.Name + " se protège. Prochains dégâts divisés par deux.");
                    views[e.Actor].color = PrototypeGui.Cyan;
                    yield return new WaitForSeconds(0.28f);
                    views[e.Actor].color = colors[e.Actor];
                    break;
                case CombatEventKind.Defeated:
                    AddLog(e.Target.Name + " neutralisé.");
                    views[e.Target].color = new Color(0.4f, 0.45f, 0.6f, 0.35f);
                    yield return new WaitForSeconds(0.2f);
                    break;
                case CombatEventKind.BattleEnded: AddLog(e.Winner == Team.Player ? "Victoire ! La zone est plus sûre." : "Défaite. Retour au point de départ avec tous tes PV."); break;
            }
        }
    }

    private IEnumerator AttackAnimation(Combatant actor, Combatant target, int amount)
    {
        var attacker = views[actor]; var defender = views[target];
        var origin = attacker.transform.position;
        var destination = origin + (defender.transform.position - origin).normalized * 1.5f;
        for (float t = 0; t < 0.16f; t += Time.deltaTime)
        { attacker.transform.position = Vector3.Lerp(origin, destination, t / 0.16f); yield return null; }
        damageText = "−" + amount;
        damagePosition = defender.transform.position + Vector3.up * 2;
        var targetOrigin = defender.transform.position;
        for (int i = 0; i < 4; i++)
        {
            defender.color = i % 2 == 0 ? new Color(1, 0.3f, 0.35f) : Color.white;
            defender.transform.position = targetOrigin + Vector3.right * (i % 2 == 0 ? 0.14f : -0.14f);
            damagePosition += Vector3.up * 0.08f;
            yield return new WaitForSeconds(0.055f);
        }
        defender.transform.position = targetOrigin; defender.color = colors[target];
        for (float t = 0; t < 0.16f; t += Time.deltaTime)
        { attacker.transform.position = Vector3.Lerp(destination, origin, t / 0.16f); yield return null; }
        attacker.transform.position = origin; damageText = null;
    }

    private void Spawn(Combatant combatant, Vector3 position, Color color, bool hero)
    {
        var view = new GameObject(combatant.Name).AddComponent<SpriteRenderer>();
        view.transform.SetParent(transform, false); view.transform.position = position;
        view.transform.localScale = Vector3.one * 1.8f;
        if (art != null)
        {
            view.sprite = art.Sprite(hero ? "Characters/astro_side_0" : "Characters/drone");
            view.sharedMaterial = art.unlit;
            var definition = GameSession.PendingEnemy != null ? GameSession.PendingEnemy : enemy;
            if (!hero && definition != null)
            {
                if (definition.battleSprite != null) view.sprite = definition.battleSprite;
                if (definition.idleFrames != null && definition.idleFrames.Length > 0)
                    view.gameObject.AddComponent<ToolboxAnimation>().Setup(view, definition.idleFrames, definition.animationFps);
            }
        }
        else
        {
            if (fallbackSprite == null) fallbackSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            view.sprite = fallbackSprite;
        }
        view.color = color; views.Add(combatant, view); colors.Add(combatant, color);
    }

    private void ReturnToMap()
    {
        bool victory = battle.Winner == Team.Player;
        GameSession.Leader.Hp = victory ? heroCombatant.Hp : GameSession.Leader.MaxHp;
        GameSession.EndEncounter(victory);
    }
    private void AddLog(string line) { log.Add(line); if (log.Count > 4) log.RemoveAt(0); }

    private void OnGUI()
    {
        if (battle == null) return;
        PrototypeGui.Begin();
        PrototypeGui.Panel(new Rect(18, 16, 924, 78));
        for (int i = 0; i < battle.Combatants.Count; i++)
        {
            var c = battle.Combatants[i]; float x = i == 0 ? 36 : 610;
            PrototypeGui.Text(new Rect(x, 24, 300, 30), c.Name + (c.IsDefending ? "  [GARDE]" : ""), 23, i == 0 ? PrototypeGui.Cyan : PrototypeGui.Gold);
            PrototypeGui.Hp(new Rect(x, 63, 208, 9), c.Hp, c.MaxHp);
            PrototypeGui.Text(new Rect(x + 220, 55, 100, 30), c.Hp + " / " + c.MaxHp, 18);
        }
        PrototypeGui.Text(new Rect(402, 36, 200, 30), "MANCHE " + battle.Round, 21, PrototypeGui.Muted);
        PrototypeGui.Panel(new Rect(18, 364, 300, 158));
        PrototypeGui.Panel(new Rect(330, 364, 612, 158));
        if (battle.IsOver && !busy)
        {
            bool won = battle.Winner == Team.Player;
            PrototypeGui.Text(new Rect(34, 373, 270, 30), won ? "VICTOIRE" : "REPLI À LA BASE", 25, PrototypeGui.Gold);
            if (PrototypeGui.Button(new Rect(34, 410, 268, 40), GameSession.HasPendingEncounter ? "[Entrée / A] Continuer" : "Recommencer"))
                pending = GameSession.HasPendingEncounter ? Command.Return : Command.Restart;
            if (!won && PrototypeGui.Button(new Rect(34, 462, 268, 40), "[R] Réessayer, PV pleins")) pending = Command.Restart;
        }
        else
        {
            PrototypeGui.Text(new Rect(34, 374, 270, 28), IsPlayerTurn ? "À TOI DE JOUER" : "ACTION EN COURS…", 22, PrototypeGui.Cyan);
            if (PrototypeGui.Button(new Rect(34, 410, 268, 40), "[Entrée / A] Attaquer", IsPlayerTurn)) pending = Command.Attack;
            if (PrototypeGui.Button(new Rect(34, 462, 268, 40), "[G / X] Se défendre", IsPlayerTurn)) pending = Command.Defend;
        }
        PrototypeGui.Text(new Rect(347, 375, 570, 28), "JOURNAL DE COMBAT", 21, PrototypeGui.Muted);
        for (int i = 0; i < log.Count; i++) PrototypeGui.Text(new Rect(347, 408 + i * 25, 576, 27), log[i], 18);
        PrototypeGui.End();
        if (damageText != null && Camera.main != null)
        {
            var p = Camera.main.WorldToScreenPoint(damagePosition);
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 20f), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = PrototypeGui.Gold;
            GUI.Label(new Rect(p.x - 70, Screen.height - p.y - 30, 140, 60), damageText, style);
        }
    }

    private void OnDestroy()
    {
        if (backgroundSprite != null) Destroy(backgroundSprite);
        if (fallbackSprite != null) Destroy(fallbackSprite);
    }
}
