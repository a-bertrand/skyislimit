using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// Petit DOM JSON pour conserver les propriétés LDtk inconnues lors d'une synchronisation.
// Aucune dépendance éditeur : sert aussi à lire les champs typés en build.
public static class ToolboxJson
{
    public static Dictionary<string, object> Object(string json) => (Dictionary<string, object>)new Reader(json).ReadDocument();
    public static Dictionary<string, object> Obj(this object value) => (Dictionary<string, object>)value;
    public static List<object> List(this object value) => value is List<object> list ? list : ((IEnumerable)value).Cast<object>().ToList();
    public static string Write(object value)
    {
        var b = new StringBuilder(); Append(b, value); return b.ToString();
    }
    private static void Append(StringBuilder b, object value)
    {
        if (value == null) { b.Append("null"); return; }
        if (value is string s)
        {
            b.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default: if (c < 32) b.Append("\\u" + ((int)c).ToString("x4")); else b.Append(c); break;
                }
            }
            b.Append('"'); return;
        }
        if (value is bool flag) { b.Append(flag ? "true" : "false"); return; }
        if (value is IDictionary<string, object> dict)
        {
            b.Append('{'); bool first = true;
            foreach (var pair in dict) { if (!first) b.Append(','); first = false; Append(b, pair.Key); b.Append(':'); Append(b, pair.Value); }
            b.Append('}'); return;
        }
        if (value is IEnumerable items)
        {
            b.Append('['); bool first = true;
            foreach (var item in items) { if (!first) b.Append(','); first = false; Append(b, item); }
            b.Append(']'); return;
        }
        b.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
    }
    private sealed class Reader
    {
        private readonly string text; private int at;
        public Reader(string text) { this.text = text ?? throw new ArgumentNullException(nameof(text)); }
        private void Space() { while (at < text.Length && char.IsWhiteSpace(text[at])) at++; }
        private char Take() { if (at >= text.Length) throw new FormatException("JSON incomplet."); return text[at++]; }
        private void Expect(char c) { Space(); if (Take() != c) throw new FormatException("JSON : attendu " + c); }
        public object ReadDocument() { var v = Value(); Space(); if (at != text.Length) throw new FormatException("JSON : caractères après le document."); return v; }
        private object Value()
        {
            Space(); if (at >= text.Length) throw new FormatException("JSON vide.");
            char c = text[at];
            if (c == '"') return String();
            if (c == '{')
            {
                at++; var d = new Dictionary<string, object>(); Space();
                if (at < text.Length && text[at] == '}') { at++; return d; }
                while (true) { Space(); string key = String(); Expect(':'); d.Add(key, Value()); Space(); c = Take(); if (c == '}') return d; if (c != ',') throw new FormatException("JSON objet invalide."); }
            }
            if (c == '[')
            {
                at++; var list = new List<object>(); Space();
                if (at < text.Length && text[at] == ']') { at++; return list; }
                while (true) { list.Add(Value()); Space(); c = Take(); if (c == ']') return list; if (c != ',') throw new FormatException("JSON tableau invalide."); }
            }
            // Comparer sur place : Substring(at) copiait toute la fin du fichier
            // trois fois pour chaque nombre d'une grille LDtk.
            if (c == 't') { Literal("true"); return true; }
            if (c == 'f') { Literal("false"); return false; }
            if (c == 'n') { Literal("null"); return null; }
            int start = at;
            while (at < text.Length && "-+0123456789.eE".IndexOf(text[at]) >= 0) at++;
            if (start == at) throw new FormatException("Valeur JSON invalide.");
            return double.Parse(text.Substring(start, at - start), CultureInfo.InvariantCulture);
        }
        private void Literal(string value)
        {
            if (at + value.Length > text.Length || string.CompareOrdinal(text, at, value, 0, value.Length) != 0)
                throw new FormatException("Valeur JSON invalide.");
            at += value.Length;
        }
        private string String()
        {
            Expect('"'); var b = new StringBuilder();
            while (true)
            {
                char c = Take(); if (c == '"') return b.ToString();
                if (c != '\\') { b.Append(c); continue; }
                c = Take();
                switch (c)
                {
                    case '"': case '\\': case '/': b.Append(c); break;
                    case 'n': b.Append('\n'); break; case 'r': b.Append('\r'); break; case 't': b.Append('\t'); break;
                    case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break;
                    case 'u': if (at + 4 > text.Length) throw new FormatException("Unicode incomplet."); b.Append((char)int.Parse(text.Substring(at, 4), NumberStyles.HexNumber)); at += 4; break;
                    default: throw new FormatException("Échappement JSON invalide.");
                }
            }
        }
    }
}
