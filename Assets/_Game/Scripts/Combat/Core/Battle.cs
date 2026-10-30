using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Combat
{
    /// <summary>
    /// Le déroulé d'un combat au tour par tour.
    /// Chaque manche, tous les combattants vivants agissent une fois, du plus rapide au plus lent.
    /// Utilisation : Start() une fois, puis Execute(action) pour chaque tour de CurrentActor.
    /// Chaque appel renvoie la liste des événements produits, à afficher dans l'ordre.
    /// </summary>
    public sealed class Battle
    {
        private readonly List<Combatant> combatants;
        private readonly Queue<Combatant> turnQueue = new Queue<Combatant>();
        private bool started;

        public IReadOnlyList<Combatant> Combatants => combatants;
        public int Round { get; private set; }
        public Combatant CurrentActor { get; private set; }
        public Team? Winner { get; private set; }
        public bool IsOver => Winner.HasValue;

        public Battle(IEnumerable<Combatant> participants)
        {
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            combatants = participants.ToList();

            if (combatants.Any(c => c == null)) throw new ArgumentException("Combattant null.", nameof(participants));
            if (!combatants.Any(c => c.Team == Team.Player) || !combatants.Any(c => c.Team == Team.Enemy))
                throw new ArgumentException("Il faut au moins un combattant dans chaque camp.", nameof(participants));
        }

        public IReadOnlyList<CombatEvent> Start()
        {
            if (started) throw new InvalidOperationException("Le combat a déjà commencé.");
            started = true;

            var events = new List<CombatEvent>();
            AdvanceTurn(events);
            return events;
        }

        public IReadOnlyList<CombatEvent> Execute(CombatAction action)
        {
            if (!started) throw new InvalidOperationException("Appelle Start() avant Execute().");
            if (IsOver) throw new InvalidOperationException("Le combat est terminé.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.Actor != CurrentActor)
                throw new InvalidOperationException($"Ce n'est pas le tour de {action.Actor.Name} mais de {CurrentActor.Name}.");

            var events = new List<CombatEvent>();

            switch (action.Kind)
            {
                case ActionKind.Attack:
                    ResolveAttack(action, events);
                    break;
                case ActionKind.Defend:
                    action.Actor.IsDefending = true;
                    events.Add(CombatEvent.Defended(action.Actor));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Action inconnue.");
            }

            if (CheckVictory(events)) return events;

            AdvanceTurn(events);
            return events;
        }

        public IEnumerable<Combatant> AliveOpponentsOf(Combatant combatant) =>
            combatants.Where(c => c.Team != combatant.Team && c.IsAlive);

        public IEnumerable<Combatant> AliveAlliesOf(Combatant combatant) =>
            combatants.Where(c => c.Team == combatant.Team && c.IsAlive);

        private void ResolveAttack(CombatAction action, List<CombatEvent> events)
        {
            var target = action.Target;
            if (!combatants.Contains(target)) throw new ArgumentException("La cible ne fait pas partie de ce combat.");
            if (!target.IsAlive) throw new InvalidOperationException($"{target.Name} est déjà hors de combat.");
            if (target == action.Actor) throw new InvalidOperationException("Un combattant ne peut pas s'attaquer lui-même.");

            var dealt = target.ApplyDamage(DamageFormula.Compute(action.Actor, target));
            events.Add(CombatEvent.Attacked(action.Actor, target, dealt));

            if (!target.IsAlive) events.Add(CombatEvent.Defeated(target));
        }

        private bool CheckVictory(List<CombatEvent> events)
        {
            var playersAlive = combatants.Any(c => c.Team == Team.Player && c.IsAlive);
            var enemiesAlive = combatants.Any(c => c.Team == Team.Enemy && c.IsAlive);
            if (playersAlive && enemiesAlive) return false;

            Winner = playersAlive ? Team.Player : Team.Enemy;
            CurrentActor = null;
            events.Add(CombatEvent.BattleEnded(Winner.Value));
            return true;
        }

        private void AdvanceTurn(List<CombatEvent> events)
        {
            while (true)
            {
                if (turnQueue.Count == 0) StartRound(events);

                var next = turnQueue.Dequeue();
                if (!next.IsAlive) continue; // tombé pendant la manche : il passe son tour

                CurrentActor = next;
                next.IsDefending = false; // la garde dure jusqu'au début de son prochain tour
                events.Add(CombatEvent.TurnStarted(next));
                return;
            }
        }

        private void StartRound(List<CombatEvent> events)
        {
            Round++;
            events.Add(CombatEvent.RoundStarted(Round));

            // OrderByDescending est stable : à vitesse égale, l'ordre de la liste départage
            foreach (var c in combatants.Where(c => c.IsAlive).OrderByDescending(c => c.Speed))
                turnQueue.Enqueue(c);
        }
    }
}
