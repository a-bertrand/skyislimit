using System;
using System.Linq;
using UnityEngine;

/// <summary>La fenêtre n'a besoin que des noms : ni validation du terrain ni accès disque au dessin.</summary>
public sealed class ToolboxLevelList
{
    [Serializable] private sealed class Index { public LevelName[] levels; }
    [Serializable] private sealed class LevelName { public string identifier; }
    private LdtkMap source;
    private string json;
    public string[] Names { get; private set; } = Array.Empty<string>();
    public string Error { get; private set; }

    public void Refresh(LdtkMap map)
    {
        var nextJson = map != null ? map.json : null;
        if (source == map && ReferenceEquals(json, nextJson)) return;
        source = map; json = nextJson;
        Names = Array.Empty<string>(); Error = null;
        if (map == null) return;
        try
        {
            var index = JsonUtility.FromJson<Index>(json);
            if (index?.levels == null) throw new FormatException("La liste des niveaux est absente. Utilise Vérifier le projet pour diagnostiquer le fichier.");
            Names = index.levels.Select(l => l.identifier).ToArray();
        }
        catch (Exception exception) { Error = exception.Message; }
    }
}
