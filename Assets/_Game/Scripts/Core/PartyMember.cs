using Game.Combat;
using UnityEngine;

/// <summary>
/// Un membre de l'équipe, en dehors des combats : ses stats et ses PV actuels.
/// Le combat en crée un Combatant (ToCombatant) puis lui renvoie ses PV à la fin.
/// Plus tard : niveau, expérience, équipement... et sauvegarde.
/// </summary>
public sealed class PartyMember
{
    private int hp;

    public string Name { get; }
    public int MaxHp { get; }
    public int Attack { get; }
    public int Defense { get; }
    public int Speed { get; }

    /// <summary>PV actuels, toujours entre 1 et MaxHp hors combat.</summary>
    public int Hp
    {
        get => hp;
        set => hp = Mathf.Clamp(value, 1, MaxHp);
    }

    public PartyMember(string name, int maxHp, int attack, int defense, int speed)
    {
        Name = name;
        MaxHp = maxHp;
        Attack = attack;
        Defense = defense;
        Speed = speed;
        hp = maxHp;
    }

    public Combatant ToCombatant() => new Combatant(Name, Team.Player, MaxHp, Attack, Defense, Speed, Hp);
}
