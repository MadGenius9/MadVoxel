using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Modding
{
    /// <summary>
    /// Reads fields off a JSON object onto a definition, one field at a time.
    ///
    /// Every read is optional: an absent key leaves the existing value alone, which is
    /// what makes patching work - a mod can change one number on a vanilla item without
    /// restating the whole thing. Keys that are never read are reported, so a typo in a
    /// mod file is a warning naming the field rather than a silently ignored line.
    /// </summary>
    public class JsonReader
    {
        readonly JsonValue _object;
        readonly ModLog _log;
        readonly string _what;
        readonly HashSet<string> _used = new HashSet<string>();

        public JsonReader(JsonValue value, ModLog log, string what)
        {
            _object = value != null && value.Kind == JsonKind.Object ? value : null;
            _log = log;
            _what = what;
        }

        public ModLog Log { get { return _log; } }

        public bool Has(string key)
        {
            return _object != null && _object.Has(key);
        }

        JsonValue Take(string key)
        {
            if (_object == null) return null;
            _used.Add(key);
            return _object[key];
        }

        void Bad(JsonValue value, string key, string expected)
        {
            _log.Error("{0}: field '{1}' on line {2} should be {3}", _what, key, value != null ? value.Line : 0, expected);
        }

        public bool Read(string key, ref string field)
        {
            var value = Take(key);
            if (value == null) return false;
            if (value.Kind != JsonKind.String) { Bad(value, key, "a string"); return false; }
            field = value.String;
            return true;
        }

        public bool Read(string key, ref bool field)
        {
            var value = Take(key);
            if (value == null) return false;
            if (value.Kind != JsonKind.Bool) { Bad(value, key, "true or false"); return false; }
            field = value.Bool;
            return true;
        }

        public bool Read(string key, ref int field)
        {
            var value = Take(key);
            if (value == null) return false;
            if (value.Kind != JsonKind.Number) { Bad(value, key, "a number"); return false; }
            field = Mathf.RoundToInt((float)value.Number);
            return true;
        }

        public bool Read(string key, ref float field)
        {
            var value = Take(key);
            if (value == null) return false;
            if (value.Kind != JsonKind.Number) { Bad(value, key, "a number"); return false; }
            field = (float)value.Number;
            return true;
        }

        public bool Read(string key, ref double field)
        {
            var value = Take(key);
            if (value == null) return false;
            if (value.Kind != JsonKind.Number) { Bad(value, key, "a number"); return false; }
            field = value.Number;
            return true;
        }

        /// <summary>Accepts "#rrggbb", "#rrggbbaa" or [r, g, b] / [r, g, b, a] in 0..1.</summary>
        public bool ReadColour(string key, ref Color field)
        {
            var value = Take(key);
            if (value == null) return false;

            if (value.Kind == JsonKind.String)
            {
                Color parsed;
                string hex = value.String.StartsWith("#") ? value.String : "#" + value.String;
                if (!ColorUtility.TryParseHtmlString(hex, out parsed))
                {
                    Bad(value, key, "a colour like \"#8a5a32\"");
                    return false;
                }
                field = parsed;
                return true;
            }

            if (value.Kind == JsonKind.Array && value.Count >= 3)
            {
                field = new Color(
                    (float)value[0].Number,
                    (float)value[1].Number,
                    (float)value[2].Number,
                    value.Count > 3 ? (float)value[3].Number : 1f);
                return true;
            }

            Bad(value, key, "a colour like \"#8a5a32\" or [0.5, 0.3, 0.2]");
            return false;
        }

        public bool ReadVector3Int(string key, ref Vector3Int field)
        {
            var value = Take(key);
            if (value == null) return false;
            if (value.Kind != JsonKind.Array || value.Count < 3) { Bad(value, key, "three numbers like [1, 2, 1]"); return false; }

            field = new Vector3Int(
                Mathf.RoundToInt((float)value[0].Number),
                Mathf.RoundToInt((float)value[1].Number),
                Mathf.RoundToInt((float)value[2].Number));
            return true;
        }

        public bool ReadEnum<T>(string key, ref T field) where T : struct
        {
            var value = Take(key);
            if (value == null) return false;
            if (value.Kind != JsonKind.String) { Bad(value, key, "one of " + Options<T>()); return false; }

            try
            {
                field = (T)Enum.Parse(typeof(T), value.String, true);
                return true;
            }
            catch (Exception)
            {
                _log.Error("{0}: '{1}' on line {2} is not a valid {3}. Try one of {4}",
                    _what, value.String, value.Line, typeof(T).Name, Options<T>());
                return false;
            }
        }

        static string Options<T>()
        {
            return string.Join(", ", Enum.GetNames(typeof(T)));
        }

        public bool ReadStringList(string key, List<string> field)
        {
            var value = Take(key);
            if (value == null) return false;
            if (value.Kind != JsonKind.Array) { Bad(value, key, "a list of strings"); return false; }

            field.Clear();
            for (int i = 0; i < value.Count; i++)
            {
                if (value[i].Kind != JsonKind.String) { Bad(value[i], key, "a list of strings"); return false; }
                field.Add(value[i].String);
            }
            return true;
        }

        /// <summary>The raw node, for shapes the helpers above do not cover.</summary>
        public JsonValue Raw(string key)
        {
            return Take(key);
        }

        /// <summary>
        /// Reports any key the definition never asked for. Almost always a typo, and
        /// almost always what a mod author is staring at when nothing happens.
        /// </summary>
        public void ReportUnknownKeys(params string[] alsoAllowed)
        {
            if (_object == null) return;

            foreach (var pair in _object.Object)
            {
                if (_used.Contains(pair.Key)) continue;

                bool allowed = false;
                for (int i = 0; i < alsoAllowed.Length; i++)
                {
                    if (alsoAllowed[i] == pair.Key) { allowed = true; break; }
                }
                if (allowed) continue;

                _log.Warn("{0}: unknown field '{1}' on line {2} was ignored", _what, pair.Key, pair.Value.Line);
            }
        }
    }
}
