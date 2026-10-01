// Reads the subset of Unity's .asset YAML that our ScriptableObjects use, and binds it
// to the real types by reflection. An unknown key means a field-name typo in the asset.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class AssetLoader
{
    public static List<string> Unknown = new List<string>();

    public static T Load<T>(string path) where T : new()
    {
        var lines = new List<string>(File.ReadAllLines(path));
        int i = 0;
        // skip the Unity header up to and including m_EditorClassIdentifier
        while (i < lines.Count && !lines[i].TrimStart().StartsWith("m_EditorClassIdentifier")) i++;
        i++;
        object o = new T();
        Bind(o, lines, ref i, 2, path);
        return (T)o;
    }

    static int Indent(string s) { int n = 0; while (n < s.Length && s[n] == ' ') n++; return n; }

    static void Bind(object target, List<string> lines, ref int i, int indent, string path)
    {
        Type t = target.GetType();
        while (i < lines.Count)
        {
            string raw = lines[i];
            if (raw.Trim().Length == 0) { i++; continue; }
            if (Indent(raw) < indent) return;

            string line = raw.Trim();
            int colon = line.IndexOf(':');
            if (colon < 0) { i++; continue; }

            string key = line.Substring(0, colon).Trim();
            string val = line.Substring(colon + 1).Trim();

            FieldInfo f = t.GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (f == null) { Unknown.Add($"{Path.GetFileName(path)}: unknown field '{key}' on {t.Name}"); i++; continue; }

            if (val.Length == 0)
            {
                i++;
                // A list of plain values, "  - \"text\"": a string[] or List<string>.
                Type scalar = f.FieldType.IsArray ? f.FieldType.GetElementType()
                            : f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>)
                                ? f.FieldType.GetGenericArguments()[0] : null;
                if (scalar != null && (scalar == typeof(string) || scalar.IsPrimitive))
                {
                    var values = new List<object>();
                    while (i < lines.Count && lines[i].Trim().StartsWith("- "))
                    {
                        values.Add(Convert(lines[i].Trim().Substring(2).Trim(), scalar));
                        i++;
                    }
                    if (f.FieldType.IsArray)
                    {
                        Array arr = Array.CreateInstance(scalar, values.Count);
                        for (int k = 0; k < values.Count; k++) arr.SetValue(values[k], k);
                        f.SetValue(target, arr);
                    }
                    else
                    {
                        var list = (IList)Activator.CreateInstance(f.FieldType);
                        foreach (object v in values) list.Add(v);
                        f.SetValue(target, list);
                    }
                    continue;
                }
                if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Type et = f.FieldType.GetGenericArguments()[0];
                    var list = (IList)Activator.CreateInstance(f.FieldType);
                    while (i < lines.Count && lines[i].Trim().StartsWith("- "))
                    {
                        // rewrite "  - k: v" as "    k: v" so the element binds like an object
                        int ind = Indent(lines[i]);
                        lines[i] = new string(' ', ind + 2) + lines[i].Trim().Substring(2);
                        object el = Activator.CreateInstance(et);
                        int save = i;
                        Bind(el, lines, ref i, ind + 2, path);
                        if (i == save) i++;
                        list.Add(el);
                    }
                    f.SetValue(target, list);
                }
                continue;
            }

            f.SetValue(target, Convert(val, f.FieldType));
            i++;
        }
    }

    static object Convert(string val, Type t)
    {
        if (t == typeof(string))
        {
            if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2)
                return System.Text.RegularExpressions.Regex.Unescape(val.Substring(1, val.Length - 2));
            return val;
        }
        if (t == typeof(bool)) return val == "1" || val == "true";
        if (t == typeof(float)) return float.Parse(val, CultureInfo.InvariantCulture);
        if (t == typeof(int)) return int.Parse(val, CultureInfo.InvariantCulture);
        if (t.IsEnum) return Enum.ToObject(t, int.Parse(val, CultureInfo.InvariantCulture));
        if (t == typeof(Vector3))
        {
            var m = System.Text.RegularExpressions.Regex.Match(val, @"x:\s*(-?[\d.eE+]+),\s*y:\s*(-?[\d.eE+]+),\s*z:\s*(-?[\d.eE+]+)");
            if (!m.Success) return new Vector3(0, 0, 0);
            return new Vector3(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                               float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                               float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
        }
        return null;   // AudioClip {fileID: 0} and friends
    }
}
