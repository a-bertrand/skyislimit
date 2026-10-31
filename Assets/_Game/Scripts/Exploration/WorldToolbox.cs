using System;
using System.Linq;
using UnityEngine;

public enum ToolboxBehaviour { Enemy, Decoration, Heal, Beacon, Sign, Portal }

[CreateAssetMenu(menuName = "RPG/Boîte à outils", fileName = "WorldToolbox")]
public sealed class WorldToolbox : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string id = "NouvelObjet";
        public ToolboxBehaviour behaviour;
        public Sprite sprite;
        public Sprite[] animation = Array.Empty<Sprite>();
        [Min(1)] public float framesPerSecond = 6;
        public Color tint = Color.white;
        public bool solid;
        public bool hover;
        public Color lightColor = Color.cyan;
        [Min(0)] public float lightIntensity;
        [Min(.1f)] public float lightRadius = 2.5f;
        public Vector2 colliderSize = new Vector2(.7f, .4f);
        public CombatantDefinition combat;
        [Range(0, 128)] public float patrolRadius = 24;
        [Range(0, 4)] public float speed = .8f;
        [TextArea] public string text = "Bienvenue !";
    }
    [HideInInspector] public int formatVersion;
    public Entry[] entries = Array.Empty<Entry>();
    public Entry Find(string id) => entries?.FirstOrDefault(e => e.id == id);
    public void Validate()
    {
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (var e in entries)
        {
            if (e == null || !System.Text.RegularExpressions.Regex.IsMatch(e.id ?? "", "^[A-Za-z][A-Za-z0-9_]*$") || !ids.Add(e.id))
                throw new FormatException("Identifiant absent, dupliqué ou invalide dans la boîte à outils.");
            if (e.id == "PlayerSpawn") throw new FormatException("PlayerSpawn est réservé au départ du joueur.");
            if (e.sprite == null) throw new FormatException(e.id + " : choisir un sprite.");
            if (e.behaviour == ToolboxBehaviour.Enemy && e.combat == null) throw new FormatException(e.id + " : choisir une fiche de combat.");
            if (e.solid && (e.colliderSize.x <= 0 || e.colliderSize.y <= 0)) throw new FormatException(e.id + " : collision invalide.");
        }
    }
}
