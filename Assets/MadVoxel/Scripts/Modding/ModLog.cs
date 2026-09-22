using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Modding
{
    public enum ModMessageKind
    {
        Info,
        Warning,
        Error
    }

    public struct ModMessage
    {
        public ModMessageKind Kind;
        public string ModId;
        public string Text;

        public override string ToString()
        {
            return string.IsNullOrEmpty(ModId) ? Text : "[" + ModId + "] " + Text;
        }
    }

    /// <summary>
    /// Collects everything the mod load had to say. Mod authors get the whole list in
    /// one place rather than hunting through the Unity console, and a bad mod never
    /// takes the game down with it - it reports and is skipped.
    /// </summary>
    public class ModLog
    {
        readonly List<ModMessage> _messages = new List<ModMessage>();

        public string CurrentModId { get; set; }

        public IReadOnlyList<ModMessage> Messages { get { return _messages; } }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        public void Info(string format, params object[] args)
        {
            Add(ModMessageKind.Info, string.Format(format, args));
        }

        public void Warn(string format, params object[] args)
        {
            WarningCount++;
            Add(ModMessageKind.Warning, string.Format(format, args));
        }

        public void Error(string format, params object[] args)
        {
            ErrorCount++;
            Add(ModMessageKind.Error, string.Format(format, args));
        }

        void Add(ModMessageKind kind, string text)
        {
            _messages.Add(new ModMessage { Kind = kind, ModId = CurrentModId, Text = text });
        }

        /// <summary>Mirrors everything to the Unity console once loading is finished.</summary>
        public void Flush()
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                var message = _messages[i];
                switch (message.Kind)
                {
                    case ModMessageKind.Error: Debug.LogError("MadVoxel mods: " + message); break;
                    case ModMessageKind.Warning: Debug.LogWarning("MadVoxel mods: " + message); break;
                    default: Debug.Log("MadVoxel mods: " + message); break;
                }
            }
        }
    }
}
