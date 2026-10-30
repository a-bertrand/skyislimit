namespace Game.Combat
{
    public enum CombatEventKind
    {
        RoundStarted,
        TurnStarted,
        Attacked,
        Defended,
        Defeated,
        BattleEnded
    }

    /// <summary>
    /// Ce qui s'est passé. Le moteur ne produit que des faits ;
    /// c'est l'affichage qui décide comment les montrer (texte, animation, son).
    /// </summary>
    public sealed class CombatEvent
    {
        public CombatEventKind Kind { get; }
        public Combatant Actor { get; }
        public Combatant Target { get; }
        public int Amount { get; }
        public int Round { get; }
        public Team? Winner { get; }

        private CombatEvent(CombatEventKind kind, Combatant actor = null, Combatant target = null,
            int amount = 0, int round = 0, Team? winner = null)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Amount = amount;
            Round = round;
            Winner = winner;
        }

        internal static CombatEvent RoundStarted(int round) => new CombatEvent(CombatEventKind.RoundStarted, round: round);
        internal static CombatEvent TurnStarted(Combatant actor) => new CombatEvent(CombatEventKind.TurnStarted, actor);
        internal static CombatEvent Attacked(Combatant actor, Combatant target, int damage) =>
            new CombatEvent(CombatEventKind.Attacked, actor, target, damage);
        internal static CombatEvent Defended(Combatant actor) => new CombatEvent(CombatEventKind.Defended, actor);
        internal static CombatEvent Defeated(Combatant target) => new CombatEvent(CombatEventKind.Defeated, target: target);
        internal static CombatEvent BattleEnded(Team winner) => new CombatEvent(CombatEventKind.BattleEnded, winner: winner);
    }
}
