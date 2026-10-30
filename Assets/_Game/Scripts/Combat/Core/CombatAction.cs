using System;

namespace Game.Combat
{
    public enum ActionKind
    {
        Attack,
        Defend
    }

    /// <summary>Ce qu'un combattant décide de faire pendant son tour.</summary>
    public sealed class CombatAction
    {
        public ActionKind Kind { get; }
        public Combatant Actor { get; }
        public Combatant Target { get; }

        private CombatAction(ActionKind kind, Combatant actor, Combatant target)
        {
            Kind = kind;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Target = target;
        }

        public static CombatAction Attack(Combatant actor, Combatant target) =>
            new CombatAction(ActionKind.Attack, actor, target ?? throw new ArgumentNullException(nameof(target)));

        public static CombatAction Defend(Combatant actor) =>
            new CombatAction(ActionKind.Defend, actor, null);
    }
}
