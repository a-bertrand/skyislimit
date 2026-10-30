using System;
using UnityEngine;

/// <summary>Références éditables, créées par Tools/RPG/Bac à sable. Les sprites existants restent partagés.</summary>
public sealed class SandboxArt : ScriptableObject
{
    [Serializable] public sealed class Entry { public string key; public Sprite sprite; }
    public Entry[] sprites;
    public Material lit, unlit;
    public Texture2D battleBackground;
    public CombatantDefinition drone, sentinel;
    public Sprite Sprite(string key) => Array.Find(sprites, s => s.key == key)?.sprite;
    public Sprite[] Frames(string direction, string suffix = "") => new[] {
        Sprite("Characters/astro_" + direction + "_0" + suffix),
        Sprite("Characters/astro_" + direction + "_1" + suffix),
        Sprite("Characters/astro_" + direction + "_2" + suffix)
    };
}
