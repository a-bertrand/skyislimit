using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using UnityEngine;

/// <summary>
/// Affichage PROTOTYPE d'un combat : relie le moteur (Game.Combat) à l'écran.
/// Aucune règle de jeu ici : ce script lit les événements du moteur et les montre.
/// Interface en IMGUI (OnGUI) pour ne rien avoir à monter dans l'éditeur ; la vraie UI viendra plus tard.
/// À poser sur un GameObject vide dans la scène Combat.
/// </summary>
public class BattleController : MonoBehaviour
{
    [Header("Données (optionnel : valeurs par défaut sinon)")]
    [SerializeField] private CombatantDefinition hero;
    [SerializeField] private CombatantDefinition enemy;

    [Header("Rythme")]
    [SerializeField] private float enemyTurnDelay = 0.8f;

    private const int MaxLogLines = 8;
    private const float VirtualHeight = 540f; // l'UI est dessinée pour 960x540 puis mise à l'échelle

    private Battle battle;
    private readonly IActionSelector enemyAI = new BasicEnemyAI();
    private readonly List<string> log = new List<string>();
    private readonly Dictionary<Combatant, SpriteRenderer> views = new Dictionary<Combatant, SpriteRenderer>();
    private readonly Dictionary<Combatant, Color> baseColors = new Dictionary<Combatant, Color>();
    private bool waitingForEnemy;
    private static Sprite squareSprite;

    // Un clic dans OnGUI est seulement mémorisé, puis exécuté dans Update :
    // modifier l'état pendant OnGUI casse la mise en page IMGUI.
    private enum PlayerCommand { None, Attack, Defend, Restart, ReturnToMap }
    private PlayerCommand pendingCommand;

    private void Start() => SetupBattle();

    private void Update()
    {
        var command = pendingCommand;
        pendingCommand = PlayerCommand.None;

        switch (command)
        {
            case PlayerCommand.Attack: PlayerAttack(); break;
            case PlayerCommand.Defend: PlayerDefend(); break;
            case PlayerCommand.Restart: SetupBattle(); break;
            case PlayerCommand.ReturnToMap: GameSession.EndEncounter(battle.Winner == Team.Player); break;
        }
    }

    private void SetupBattle()
    {
        StopAllCoroutines();
        foreach (var view in views.Values) Destroy(view.gameObject);
        views.Clear();
        baseColors.Clear();
        log.Clear();
        waitingForEnemy = false;

        var h = hero != null ? hero.Create(Team.Player) : new Combatant("Héros", Team.Player, 40, 12, 6, 10);
        // L'ennemi touché sur la carte est prioritaire sur celui réglé dans l'Inspector
        var enemyDef = GameSession.PendingEnemy != null ? GameSession.PendingEnemy : enemy;
        var e = enemyDef != null ? enemyDef.Create(Team.Enemy) : new Combatant("Drone pirate", Team.Enemy, 50, 11, 4, 8);

        battle = new Battle(new[] { h, e });
        // Positions décalées vers la droite pour laisser la place au panneau d'interface à gauche
        SpawnView(h, new Vector3(0f, 0f, 0f), hero != null ? hero.placeholderColor : new Color(0.35f, 0.65f, 1f));
        SpawnView(e, new Vector3(4.5f, 0f, 0f), enemyDef != null ? enemyDef.placeholderColor : new Color(1f, 0.4f, 0.4f));

        Handle(battle.Start());
    }

    // ---------- Actions du joueur ----------

    private bool IsPlayerTurn =>
        battle != null && !battle.IsOver && !waitingForEnemy && battle.CurrentActor.Team == Team.Player;

    private void PlayerAttack()
    {
        if (!IsPlayerTurn) return;
        var actor = battle.CurrentActor;
        var target = battle.AliveOpponentsOf(actor).First(); // un seul ennemi pour l'instant
        Handle(battle.Execute(CombatAction.Attack(actor, target)));
    }

    private void PlayerDefend()
    {
        if (!IsPlayerTurn) return;
        Handle(battle.Execute(CombatAction.Defend(battle.CurrentActor)));
    }

    // ---------- Boucle : afficher les événements, puis faire jouer l'ennemi si c'est son tour ----------

    private void Handle(IReadOnlyList<CombatEvent> events)
    {
        foreach (var ev in events) Show(ev);

        if (!battle.IsOver && battle.CurrentActor.Team == Team.Enemy)
            StartCoroutine(EnemyTurn());
    }

    private IEnumerator EnemyTurn()
    {
        waitingForEnemy = true;
        yield return new WaitForSeconds(enemyTurnDelay);
        waitingForEnemy = false;

        var action = enemyAI.Choose(battle, battle.CurrentActor);
        Handle(battle.Execute(action));
    }

    private void Show(CombatEvent ev)
    {
        switch (ev.Kind)
        {
            case CombatEventKind.RoundStarted:
                AddLog($"— Manche {ev.Round} —");
                break;
            case CombatEventKind.Attacked:
                AddLog($"{ev.Actor.Name} attaque {ev.Target.Name} : {ev.Amount} dégâts");
                StartCoroutine(HitFlash(ev.Target));
                break;
            case CombatEventKind.Defended:
                AddLog($"{ev.Actor.Name} se met en garde");
                break;
            case CombatEventKind.Defeated:
                AddLog($"{ev.Target.Name} est hors de combat !");
                if (views.TryGetValue(ev.Target, out var view)) view.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                break;
            case CombatEventKind.BattleEnded:
                AddLog(ev.Winner == Team.Player ? "VICTOIRE !" : "Défaite...");
                break;
            // TurnStarted : rien à écrire, l'interface affiche déjà à qui c'est le tour
        }
    }

    private void AddLog(string line)
    {
        log.Add(line);
        if (log.Count > MaxLogLines) log.RemoveAt(0);
    }

    // ---------- Visuels placeholder ----------

    private void SpawnView(Combatant combatant, Vector3 position, Color color)
    {
        if (squareSprite == null)
            squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

        var go = new GameObject(combatant.Name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 1.5f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = color;

        views[combatant] = sr;
        baseColors[combatant] = color;
    }

    private IEnumerator HitFlash(Combatant target)
    {
        if (!views.TryGetValue(target, out var view)) yield break;

        var origin = view.transform.position;
        for (var i = 0; i < 4; i++)
        {
            view.color = i % 2 == 0 ? Color.white : baseColors[target];
            view.transform.position = origin + new Vector3(i % 2 == 0 ? 0.15f : -0.15f, 0f, 0f);
            yield return new WaitForSeconds(0.05f);
        }
        view.transform.position = origin;
        view.color = target.IsAlive ? baseColors[target] : new Color(0.3f, 0.3f, 0.3f, 0.5f);
    }

    // ---------- Interface prototype (IMGUI) ----------

    private void OnGUI()
    {
        if (battle == null) return;

        var scale = Screen.height / VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        GUILayout.BeginArea(new Rect(20, 20, 300, VirtualHeight - 40));

        foreach (var c in battle.Combatants)
        {
            var guard = c.IsDefending ? "   [en garde]" : "";
            GUILayout.Label($"{c.Name}   PV {c.Hp}/{c.MaxHp}{guard}");
        }

        GUILayout.Space(12);

        if (battle.IsOver)
        {
            var won = battle.Winner == Team.Player;
            GUILayout.Label(won ? "Combat gagné !" : "Combat perdu...");

            if (GameSession.HasPendingEncounter)
            {
                // Lancé depuis la carte : on peut y retourner
                if (!won && GUILayout.Button("Réessayer", GUILayout.Height(32))) pendingCommand = PlayerCommand.Restart;
                if (GUILayout.Button(won ? "Continuer" : "Retour à la carte", GUILayout.Height(32)))
                    pendingCommand = PlayerCommand.ReturnToMap;
            }
            else if (GUILayout.Button("Recommencer", GUILayout.Height(32)))
            {
                // Scène Combat lancée seule depuis l'éditeur (test)
                pendingCommand = PlayerCommand.Restart;
            }
        }
        else if (IsPlayerTurn)
        {
            GUILayout.Label($"À toi : que fait {battle.CurrentActor.Name} ?");
            if (GUILayout.Button("Attaquer", GUILayout.Height(32))) pendingCommand = PlayerCommand.Attack;
            if (GUILayout.Button("Se défendre", GUILayout.Height(32))) pendingCommand = PlayerCommand.Defend;
        }
        else
        {
            GUILayout.Label($"{battle.CurrentActor.Name} réfléchit...");
        }

        GUILayout.Space(12);
        foreach (var line in log) GUILayout.Label(line);

        GUILayout.EndArea();
    }
}
