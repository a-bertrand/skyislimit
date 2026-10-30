using System;
using System.Linq;

namespace Game.Combat
{
    /// <summary>Choisit l'action d'un combattant contrôlé par l'ordinateur.</summary>
    public interface IActionSelector
    {
        CombatAction Choose(Battle battle, Combatant actor);
    }

    /// <summary>IA v1 : attaque toujours l'adversaire vivant qui a le moins de PV.</summary>
    public sealed class BasicEnemyAI : IActionSelector
    {
        public CombatAction Choose(Battle battle, Combatant actor)
        {
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            if (actor == null) throw new ArgumentNullException(nameof(actor));

            var target = battle.AliveOpponentsOf(actor).OrderBy(c => c.Hp).FirstOrDefault();
            return target != null ? CombatAction.Attack(actor, target) : CombatAction.Defend(actor);
        }
    }
}
