using System;

namespace MadVoxel.Core
{
    /// <summary>
    /// One-line player feedback ("+3 Wood", "Claim placed"). Systems post, the HUD
    /// listens; nothing gameplay-side needs a reference to the UI.
    /// </summary>
    public static class Notifications
    {
        public static event Action<string> Posted;

        public static void Post(string message)
        {
            if (Posted != null) Posted(message);
        }

        public static void PostFormat(string format, params object[] args)
        {
            Post(string.Format(format, args));
        }
    }
}
