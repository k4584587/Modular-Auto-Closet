#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace needon.Editor.Util
{
    internal static class ClosetLocalization
    {
        private const string LocalizationPath = "Packages/kr.needon.modular-auto-closet/Resource/Localization.json";

        private static Dictionary<string, Dictionary<string, string>> _table; // lang -> key -> value
        private static DateTime _lastLoad = DateTime.MinValue;

        private static void EnsureLoaded()
        {
            // Simple cache. Can be extended to watch file changes.
            if (_table != null) return;
            Reload();
        }

        public static void Reload()
        {
            _table = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(LocalizationPath);
            if (text == null)
            {
                Debug.LogWarning($"Localization file not found at: {LocalizationPath}");
                return;
            }
            try
            {
                var root = JsonUtility.FromJson<Wrapper>(WrapJson(text.text));
                if (root != null && root.items != null)
                {
                    foreach (var lang in root.items)
                    {
                        _table[lang.code] = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var entry in lang.entries)
                        {
                            _table[lang.code][entry.key] = entry.value;
                        }
                    }
                }
                _lastLoad = DateTime.Now;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse localization JSON: {e.Message}");
            }
        }

        // Unity's JsonUtility doesn't support dictionaries; convert to arrays
        [Serializable]
        private class Wrapper { public LangBlock[] items; }
        [Serializable]
        private class LangBlock { public string code; public Entry[] entries; }
        [Serializable]
        private class Entry { public string key; public string value; }

        private static string WrapJson(string raw)
        {
            // raw shape: { "en": {"K":"V"}, "ko": {...} }
            // convert to: { "items": [ {"code":"en", "entries":[{"key":"K","value":"V"}, ...]}, ... ] }
            // Do a quick-and-safe transform via MiniJSON or manual parsing; here we use EditorJsonUtility twice.
            // For simplicity and robustness, fall back to a very small JSON walker using JsonUtility by stages.

            // Use an auxiliary Mini parser via JSON.Net is not available; so do a lightweight conversion via Unity's
            // Json for known structure by re-parsing with EditorJsonUtility and a Temporary ScriptableObject.

            // Because writing a full converter is overkill, we will use a tiny runtime method:
            var dict = SimpleJson.Parse(raw);
            var items = new System.Text.StringBuilder();
            items.Append("{\"items\":[");
            bool firstLang = true;
            foreach (var kv in dict)
            {
                if (!firstLang) items.Append(',');
                firstLang = false;
                items.Append("{\"code\":\"").Append(kv.Key).Append("\",\"entries\":[");
                bool firstEntry = true;
                foreach (var kv2 in kv.Value)
                {
                    if (!firstEntry) items.Append(',');
                    firstEntry = false;
                    items.Append("{\"key\":\"").Append(Escape(kv2.Key)).Append("\",\"value\":\"")
                         .Append(Escape(kv2.Value)).Append("\"}");
                }
                items.Append("]}");
            }
            items.Append("]}");
            return items.ToString();
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        public static string Get(UnityEngine.Object context, string key, params object[] args)
        {
            var lang = ResolveLanguage(context);
            return Get(lang, key, args);
        }

        public static string Get(AutoCloset.ClosetLanguage lang, string key, params object[] args)
        {
            EnsureLoaded();
            var code = ToCode(lang);
            string text = null;
            if (_table != null)
            {
                if (_table.TryGetValue(code, out var dict) && dict.TryGetValue(key, out var v))
                    text = v;
                else if (_table.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var en))
                    text = en;
            }
            text ??= key; // fallback to key
            try { return (args != null && args.Length > 0) ? string.Format(text, args) : text; }
            catch { return text; }
        }

        private static string ToCode(AutoCloset.ClosetLanguage lang)
        {
            switch (lang)
            {
                case AutoCloset.ClosetLanguage.Korean: return "ko";
                case AutoCloset.ClosetLanguage.Japanese: return "ja";
                default: return "en";
            }
        }

        private static AutoCloset.ClosetLanguage ResolveLanguage(UnityEngine.Object context)
        {
            try
            {
                if (context is Component comp)
                {
                    var t = comp.transform;
                    while (t != null)
                    {
                        var c = t.GetComponent<AutoCloset>();
                        if (c != null) return c.language;
                        t = t.parent;
                    }
                }
                else if (context is GameObject go)
                {
                    var t = go.transform;
                    while (t != null)
                    {
                        var c = t.GetComponent<AutoCloset>();
                        if (c != null) return c.language;
                        t = t.parent;
                    }
                }
                var any = UnityEngine.Object.FindObjectOfType<AutoCloset>();
                if (any != null) return any.language;
            }
            catch { }
            return AutoCloset.ClosetLanguage.English;
        }

        // Minimal JSON helper to read top-level { lang: { key: value } }
        private static class SimpleJson
        {
            public static Dictionary<string, Dictionary<string, string>> Parse(string json)
            {
                var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var root = MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
                    if (root == null) return result;
                    foreach (var langPair in root)
                    {
                        var langDict = new Dictionary<string, string>();
                        var inner = langPair.Value as Dictionary<string, object>;
                        if (inner != null)
                        {
                            foreach (var kv in inner)
                                langDict[kv.Key] = kv.Value?.ToString() ?? string.Empty;
                        }
                        result[langPair.Key] = langDict;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Localization JSON parse error: {e.Message}");
                }
                return result;
            }
        }

        // Minimal embedded MiniJSON (Unity MIT) to avoid external deps
        private static class MiniJSON
        {
            public static class Json
            {
                public static object Deserialize(string json) => Parser.Parse(json);

                private sealed class Parser : IDisposable
                {
                    private const string WORD_BREAK = "{}[],:\"";
                    private StringReader _json;
                    private Parser(string json) { _json = new StringReader(json); }
                    public static object Parse(string json) { using (var p = new Parser(json)) return p.ParseValue(); }
                    public void Dispose() { _json.Dispose(); _json = null; }
                    private Dictionary<string, object> ParseObject()
                    {
                        var table = new Dictionary<string, object>();
                        _json.Read();
                        while (true)
                        {
                            EatWhitespace();
                            if (PeekChar == '}') { _json.Read(); break; }
                            var key = ParseString();
                            EatWhitespace(); _json.Read(); // :
                            var value = ParseValue();
                            table[key] = value;
                            EatWhitespace();
                            if (PeekChar == ',') { _json.Read(); continue; }
                            if (PeekChar == '}') { _json.Read(); break; }
                        }
                        return table;
                    }
                    private List<object> ParseArray()
                    {
                        var array = new List<object>();
                        _json.Read();
                        while (true)
                        {
                            EatWhitespace();
                            if (PeekChar == ']') { _json.Read(); break; }
                            array.Add(ParseValue());
                            EatWhitespace();
                            if (PeekChar == ',') { _json.Read(); continue; }
                            if (PeekChar == ']') { _json.Read(); break; }
                        }
                        return array;
                    }
                    private object ParseValue()
                    {
                        EatWhitespace();
                        switch (PeekChar)
                        {
                            case '"': return ParseString();
                            case '{': return ParseObject();
                            case '[': return ParseArray();
                            case 't': Next(4); return true;
                            case 'f': Next(5); return false;
                            case 'n': Next(4); return null;
                        }
                        return ParseNumber();
                    }
                    private string ParseString()
                    {
                        var s = new System.Text.StringBuilder();
                        _json.Read();
                        while (_json.Peek() != -1)
                        {
                            char c = NextChar;
                            if (c == '"') break;
                            if (c == '\\')
                            {
                                c = NextChar;
                                if (c == '"' || c == '\\' || c == '/') s.Append(c);
                                else if (c == 'b') s.Append('\b');
                                else if (c == 'f') s.Append('\f');
                                else if (c == 'n') s.Append('\n');
                                else if (c == 'r') s.Append('\r');
                                else if (c == 't') s.Append('\t');
                                else if (c == 'u')
                                {
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                }
                            }
                            else s.Append(c);
                        }
                        return s.ToString();
                    }
                    private object ParseNumber()
                    {
                        var num = NextWord;
                        if (num.IndexOf('.') != -1 || num.IndexOf('e') != -1 || num.IndexOf('E') != -1)
                        {
                            if (double.TryParse(num, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                                return d;
                        }
                        if (long.TryParse(num, out var l)) return l;
                        return 0;
                    }
                    private void EatWhitespace() { while (char.IsWhiteSpace(PeekChar)) _json.Read(); }
                    private char PeekChar => _json.Peek() != -1 ? Convert.ToChar(_json.Peek()) : '\0';
                    private char NextChar => Convert.ToChar(_json.Read());
                    private string NextWord { get { var s = new System.Text.StringBuilder(); while (!IsWordBreak(PeekChar)) { s.Append(NextChar); if (_json.Peek() == -1) break; } return s.ToString(); } }
                    private void Next(int count) { for (int i = 0; i < count; i++) _json.Read(); }
                    private static bool IsWordBreak(char c) => char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;
                }
            }
        }
    }
}
#endif
