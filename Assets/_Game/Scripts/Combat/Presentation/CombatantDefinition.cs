using Game.Combat;
using UnityEngine;

/// <summary>
/// Fiche de données d'un combattant (ScriptableObject), éditable dans l'Inspector.
/// Création : clic droit dans Project > Create > RPG > Combattant.
/// </summary>
[CreateAssetMenu(menuName = "RPG/Combattant", fileName = "NouveauCombattant")]
public class CombatantDefinition : ScriptableObject
{
    public Sprite battleSprite;
    public Sprite[] idleFrames;
    [Min(1)] public float animationFps = 6;
    public string displayName = "Combattant";
    [Min(1)] public int maxHp = 40;
    [Min(0)] public int attack = 10;
    [Min(0)] public int defense = 5;
    public int speed = 10;
    public Color placeholderColor = Color.white;

    /// <summary>Crée l'instance utilisée par le moteur de combat (C# pur).</summary>
    public Combatant Create(Team team) =>
        new Combatant(displayName, team, maxHp, attack, defense, speed);
}
