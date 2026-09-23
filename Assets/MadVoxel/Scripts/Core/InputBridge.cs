using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// One place that talks to UnityEngine.Input. Keeps the rest of the game free of
    /// input-backend details so swapping to the Input System later is a single file.
    /// Requires Active Input Handling = "Input Manager (Old)" or "Both"
    /// (MadVoxel &gt; Setup &gt; Configure Project sets this for you).
    /// </summary>
    public static class InputBridge
    {
        public static bool Enabled = true;

        public static Vector2 Move
        {
            get
            {
                if (!Enabled) return Vector2.zero;
                float x = (Key(KeyCode.D) ? 1f : 0f) - (Key(KeyCode.A) ? 1f : 0f);
                float y = (Key(KeyCode.W) ? 1f : 0f) - (Key(KeyCode.S) ? 1f : 0f);
                Vector2 v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static Vector2 Look
        {
            get
            {
                if (!Enabled) return Vector2.zero;
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            }
        }

        public static float ScrollDelta
        {
            get { return Enabled ? Input.mouseScrollDelta.y : 0f; }
        }

        public static bool Jump { get { return Enabled && Key(KeyCode.Space); } }
        public static bool Sprint { get { return Enabled && (Key(KeyCode.LeftShift) || Key(KeyCode.RightShift)); } }
        public static bool Crouch { get { return Enabled && (Key(KeyCode.LeftControl) || Key(KeyCode.C)); } }

        public static bool PrimaryHeld { get { return Enabled && Input.GetMouseButton(0); } }
        public static bool PrimaryDown { get { return Enabled && Input.GetMouseButtonDown(0); } }
        public static bool SecondaryDown { get { return Enabled && Input.GetMouseButtonDown(1); } }
        public static bool InteractDown { get { return Enabled && Input.GetKeyDown(KeyCode.E); } }
        public static bool RotatePieceDown { get { return Enabled && Input.GetKeyDown(KeyCode.R); } }

        /// <summary>Lower or raise the implement on the back of the machine you are driving.</summary>
        public static bool ImplementToggleDown { get { return Enabled && Input.GetKeyDown(KeyCode.F); } }
        /// <summary>Hitch what you are carrying, or drop what is hitched.</summary>
        public static bool HitchDown { get { return Enabled && Input.GetKeyDown(KeyCode.G); } }
        /// <summary>Fill or empty the hopper: seed in, harvest out.</summary>
        public static bool HopperDown { get { return Enabled && Input.GetKeyDown(KeyCode.V); } }

        // Menu keys stay live even when gameplay input is suppressed.
        public static bool InventoryDown { get { return Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I); } }
        public static bool PerksDown { get { return Input.GetKeyDown(KeyCode.P); } }
        public static bool PauseDown { get { return Input.GetKeyDown(KeyCode.Escape); } }
        public static bool DebugDown { get { return Input.GetKeyDown(KeyCode.F3); } }

        public static int HotbarDigitDown()
        {
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
            }
            return -1;
        }

        static bool Key(KeyCode code)
        {
            return Input.GetKey(code);
        }
    }
}
