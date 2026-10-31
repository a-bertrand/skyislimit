using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Mesure relançable sur le vrai projet et contrôles de fraîcheur de la liste affichée.</summary>
public static class ToolboxPerformanceChecks
{
    [MenuItem("Tools/RPG/Mesurer les performances de la boîte à outils")]
    public static void Run()
    {
        var results = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            results.Add("PASS " + name);
        }
        LdtkMap copy = null;
        try
        {
            var map = AssetDatabase.LoadAssetAtPath<LdtkMap>(ToolboxDemo.MapPath);
            map.Read(); // Échauffer JsonUtility et le JIT avant la mesure.
            var watch = Stopwatch.StartNew();
            const int count = 5;
            for (int i = 0; i < count; i++) map.Read();
            watch.Stop();
            results.Add("Lecture complète de " + map.name + " (" + map.json.Length + " caractères), moyenne sur " + count + " lectures : " +
                (watch.Elapsed.TotalMilliseconds / count).ToString("F3") + " ms");

            var cache = new ToolboxLevelList(); cache.Refresh(map);
            var names = cache.Names;
            Check(cache.Error == null && names.SequenceEqual(map.Read().levels.Select(l => l.identifier)), "Liste affichée identique aux niveaux du projet");
            watch.Restart();
            for (int i = 0; i < 10000; i++) cache.Refresh(map);
            watch.Stop();
            results.Add("10 000 consultations de la liste mise en cache : " + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
            Check(ReferenceEquals(names, cache.Names), "Les rafraîchissements réutilisent la liste sans relire la carte");

            copy = UnityEngine.Object.Instantiate(map);
            copy.json = ToolboxSync.AddLevel(copy.json, "PerformanceCheck_" + Guid.NewGuid().ToString("N"));
            cache.Refresh(copy);
            Check(cache.Names.Length == names.Length + 1, "Changement de projet visible immédiatement");
            copy.json = copy.json.Replace("PerformanceCheck_", "NiveauRenomme_");
            cache.Refresh(copy);
            Check(cache.Names.Last().StartsWith("NiveauRenomme_", StringComparison.Ordinal), "Une modification du JSON importé actualise les noms");
            copy.json = "{fichier invalide"; cache.Refresh(copy);
            Check(cache.Error != null && cache.Names.Length == 0, "Erreur affichée sans conserver une ancienne liste");
            string error = cache.Error; cache.Refresh(copy);
            Check(ReferenceEquals(error, cache.Error), "Un fichier invalide n'est pas reparsé à chaque rafraîchissement");
            copy.json = map.json; cache.Refresh(copy);
            Check(cache.Error == null && cache.Names.SequenceEqual(names), "Liste restaurée quand le JSON est corrigé");
            cache.Refresh(null);
            Check(cache.Error == null && cache.Names.Length == 0, "Désélection du projet sans liste résiduelle");

            var values = ToolboxJson.Object("{\"a\":[true,false,null,1,-2.5,1e3],\"text\":\"Étoile \\u2605\"}");
            Check((bool)values["a"].List()[0] && !(bool)values["a"].List()[1] && values["a"].List()[2] == null &&
                (double)values["a"].List()[5] == 1000 && (string)values["text"] == "Étoile ★", "Lecture des littéraux, nombres et Unicode conservée");
            foreach (var literal in new[] { "tru", "fals", "nul", "truth", "falsex" })
            {
                bool rejected = false;
                try { ToolboxJson.Object("{\"v\":" + literal + "}"); } catch (FormatException) { rejected = true; }
                Check(rejected, "Littéral invalide refusé : " + literal);
            }
            results.Add("SUCCESS");
        }
        catch (Exception exception) { results.Add("FAIL " + exception); UnityEngine.Debug.LogException(exception); }
        finally
        {
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/toolbox-performance.txt", results);
            UnityEngine.Debug.Log("[Performances boîte à outils] " + string.Join("\n", results));
        }
    }
}
