using System;

namespace Game.Combat
{
    public enum Team
    {
        Player,
        Enemy
    }

    /// <summary>
    /// Un participant au combat (perso, ennemi... et plus tard vaisseau).
    /// C# pur : aucune dépendance à Unity, donc testable sans lancer le jeu.
    /// </summary>
    public sealed class Combatant
    {
        public string Name { get; }
        public Team Team { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public int Attack { get; }
        public int Defense { get; }
        public int Speed { get; }

        /// <summary>En garde : dégâts reçus divisés par deux jusqu'à son prochain tour.</summary>
        public bool IsDefending { get; internal set; }

        public bool IsAlive => Hp > 0;

        public Combatant(string name, Team team, int maxHp, int attack, int defense, int speed)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Le nom est obligatoire.", nameof(name));
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp), "Les PV max doivent être > 0.");
            if (attack < 0) throw new ArgumentOutOfRangeException(nameof(attack));
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense));

            Name = name;
            Team = team;
            MaxHp = maxHp;
            Hp = maxHp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
        }

        /// <summary>Retire des PV sans descendre sous 0. Renvoie les dégâts réellement infligés.</summary>
        internal int ApplyDamage(int amount)
        {
            var dealt = Math.Min(Math.Max(0, amount), Hp);
            Hp -= dealt;
            return dealt;
        }

        public override string ToString() => $"{Name} ({Hp}/{MaxHp} PV)";
    }
}
