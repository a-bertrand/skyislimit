using System;

namespace Game.Combat
{
    /// <summary>
    /// Formule de dégâts, isolée pour pouvoir l'équilibrer et la tester facilement.
    /// v1 : attaque - défense/2, divisée par 2 si la cible est en garde, minimum 1.
    /// </summary>
    public static class DamageFormula
    {
        public static int Compute(Combatant attacker, Combatant target)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var damage = attacker.Attack - target.Defense / 2;
            if (target.IsDefending) damage /= 2;
            return Math.Max(1, damage);
        }
    }
}
