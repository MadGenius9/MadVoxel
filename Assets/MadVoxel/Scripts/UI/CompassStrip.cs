using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The one thing Claim Slate allows near the top of the screen: a thin tape of
    /// bearings with the day and clock stamped inside it. It replaces a minimap - you
    /// get a direction and a distance, never a top-down picture of ground you have not
    /// walked. Pips are shaped as well as coloured, so the four kinds stay apart for a
    /// colourblind player.
    /// </summary>
    public class CompassStrip : MonoBehaviour
    {
        public enum PipKind { Trader, Claim, Bed, Threat, Board }

        const float Width = 760f;
        const float Height = 62f;
        const float VisibleDegrees = 120f;
        const float PixelsPerDegree = Width / VisibleDegrees;
        const int PipPoolPerKind = 8;

        RectTransform _root;
        RectTransform _tape;
        Text _clockLabel;
        Image _nightMark;

        readonly List<RectTransform> _ticks = new List<RectTransform>();
        readonly List<float> _tickBearings = new List<float>();
        readonly List<RectTransform> _cardinals = new List<RectTransform>();
        readonly List<float> _cardinalBearings = new List<float>();

        readonly Dictionary<PipKind, List<RectTransform>> _pips = new Dictionary<PipKind, List<RectTransform>>();
        readonly Dictionary<PipKind, int> _pipCursor = new Dictionary<PipKind, int>();

        float _heading;

        public void Build(Transform parent)
        {
            var plate = ClaimSlate.Fill(parent, "Compass", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.55f));
            ClaimSlate.Place(plate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(Width, Height));
            _root = plate.rectTransform;

            // Only a top and bottom rule: a full box here would read as a panel, and the
            // centre of the screen has to stay the world.
            var top = ClaimSlate.Fill(_root, "RuleTop", ClaimSlate.Dim(ClaimSlate.Bone, 0.30f));
            ClaimSlate.Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width, 2f));

            var bottom = ClaimSlate.Fill(_root, "RuleBottom", ClaimSlate.Dim(ClaimSlate.Bone, 0.18f));
            ClaimSlate.Place(bottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(Width, 1f));

            var viewport = ClaimSlate.Rect(_root, "Tape");
            ClaimSlate.Place(viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(Width, 34f));
            viewport.gameObject.AddComponent<RectMask2D>();
            _tape = viewport;

            BuildTicks();
            BuildCardinals();
            BuildPipPools();
            BuildClock();

            // The heading notch: where "straight ahead" is. Rust, and the only rust on
            // the strip, so the eye lands on it first.
            var notch = ClaimSlate.Fill(_root, "Notch", ClaimSlate.OxideRust);
            ClaimSlate.Place(notch.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(2f, 16f));
        }

        void BuildTicks()
        {
            for (int degrees = 0; degrees < 360; degrees += 15)
            {
                bool major = degrees % 45 == 0;
                var tick = ClaimSlate.Fill(_tape, "Tick" + degrees,
                    ClaimSlate.Dim(ClaimSlate.Bone, major ? 0.55f : 0.28f));
                ClaimSlate.Place(tick.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    Vector2.zero, new Vector2(major ? 2f : 1f, major ? 12f : 7f));

                _ticks.Add(tick.rectTransform);
                _tickBearings.Add(degrees);
            }
        }

        void BuildCardinals()
        {
            AddCardinal("N", 0f);
            AddCardinal("E", 90f);
            AddCardinal("S", 180f);
            AddCardinal("W", 270f);
        }

        void AddCardinal(string letter, float bearing)
        {
            var label = ClaimSlate.Stencil(_tape, "Card" + letter, letter, 19, TextAnchor.LowerCenter, ClaimSlate.Bone);
            var holder = ClaimSlate.Holder(label);
            ClaimSlate.Place(holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(40f, 22f));

            _cardinals.Add(holder);
            _cardinalBearings.Add(bearing);
        }

        void BuildPipPools()
        {
            foreach (PipKind kind in System.Enum.GetValues(typeof(PipKind)))
            {
                var pool = new List<RectTransform>();
                for (int i = 0; i < PipPoolPerKind; i++)
                {
                    var pip = MakePip(kind, i);
                    pip.gameObject.SetActive(false);
                    pool.Add(pip);
                }
                _pips[kind] = pool;
                _pipCursor[kind] = 0;
            }
        }

        /// <summary>Shape carries the meaning; colour only reinforces it.</summary>
        RectTransform MakePip(PipKind kind, int index)
        {
            string name = kind + "Pip" + index;

            switch (kind)
            {
                case PipKind.Trader:
                {
                    // A gold diamond: a square turned on its point, so it cannot be
                    // mistaken for the claim square even in greyscale.
                    var pip = ClaimSlate.Fill(_tape, name, ClaimSlate.SodiumGold);
                    ClaimSlate.Place(pip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 4f), new Vector2(9f, 9f));
                    pip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    return pip.rectTransform;
                }
                case PipKind.Claim:
                {
                    var pip = ClaimSlate.Fill(_tape, name, ClaimSlate.OxideRust);
                    ClaimSlate.Place(pip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 5f), new Vector2(9f, 9f));
                    return pip.rectTransform;
                }
                case PipKind.Bed:
                {
                    var pip = ClaimSlate.Fill(_tape, name, ClaimSlate.CropSage);
                    ClaimSlate.Place(pip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 5f), new Vector2(14f, 4f));
                    return pip.rectTransform;
                }
                case PipKind.Board:
                {
                    // Bone, and an upright: the only pip that is taller than it is wide
                    // and not blood-coloured, so the colony never reads as a threat.
                    var pip = ClaimSlate.Fill(_tape, name, ClaimSlate.Bone);
                    ClaimSlate.Place(pip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 3f), new Vector2(4f, 13f));
                    return pip.rectTransform;
                }
                default:
                {
                    var pip = ClaimSlate.Fill(_tape, name, ClaimSlate.Blood);
                    ClaimSlate.Place(pip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -1f), new Vector2(3f, 18f));
                    return pip.rectTransform;
                }
            }
        }

        void BuildClock()
        {
            _clockLabel = ClaimSlate.Stencil(_root, "DayClock", "", 22, TextAnchor.LowerCenter, ClaimSlate.SodiumGold);
            ClaimSlate.Place(ClaimSlate.Holder(_clockLabel), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 4f), new Vector2(Width, 24f));

            // A sodium lamp at dusk: the mark beside the clock that says the sun is down
            // without another word of text.
            _nightMark = ClaimSlate.Fill(_root, "NightMark", ClaimSlate.SodiumGold);
            ClaimSlate.Place(_nightMark.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-128f, 13f), new Vector2(7f, 7f));
            _nightMark.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ driving

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
        }

        /// <summary>Yaw in degrees, the way the player is facing.</summary>
        public void SetHeading(float yawDegrees)
        {
            _heading = yawDegrees;

            for (int i = 0; i < _ticks.Count; i++) PlaceOnTape(_ticks[i], _tickBearings[i]);
            for (int i = 0; i < _cardinals.Count; i++) PlaceOnTape(_cardinals[i], _cardinalBearings[i]);
        }

        public void SetClock(string dayClock, bool night)
        {
            if (_clockLabel != null)
            {
                _clockLabel.text = dayClock;
                _clockLabel.color = night ? ClaimSlate.Dim(ClaimSlate.SodiumGold, 0.72f) : ClaimSlate.SodiumGold;
            }
            if (_nightMark != null) _nightMark.gameObject.SetActive(night);
        }

        static readonly PipKind[] AllKinds =
            (PipKind[])System.Enum.GetValues(typeof(PipKind));

        /// <summary>Call once per frame, then AddPip for each marker, then EndPips.</summary>
        public void BeginPips()
        {
            for (int i = 0; i < AllKinds.Length; i++) _pipCursor[AllKinds[i]] = 0;
        }

        public void AddPip(PipKind kind, float bearingDegrees)
        {
            var pool = _pips[kind];
            int cursor = _pipCursor[kind];
            if (cursor >= pool.Count) return;

            var pip = pool[cursor];
            _pipCursor[kind] = cursor + 1;

            if (!PlaceOnTape(pip, bearingDegrees))
            {
                // Off the tape: hand the slot straight back rather than burning it.
                _pipCursor[kind] = cursor;
            }
        }

        public void EndPips()
        {
            foreach (var entry in _pips)
            {
                int used = _pipCursor[entry.Key];
                var pool = entry.Value;
                for (int i = used; i < pool.Count; i++)
                {
                    if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Returns false when the bearing falls outside the visible arc.</summary>
        bool PlaceOnTape(RectTransform rect, float bearing)
        {
            float x;
            if (!CompassMath.TryPlace(_heading, bearing, VisibleDegrees, PixelsPerDegree, out x))
            {
                if (rect.gameObject.activeSelf) rect.gameObject.SetActive(false);
                return false;
            }

            if (!rect.gameObject.activeSelf) rect.gameObject.SetActive(true);
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            return true;
        }

        /// <summary>Bearing from one world point to another, in compass degrees.</summary>
        public static float BearingTo(Vector3 from, Vector3 to)
        {
            return CompassMath.Bearing(from, to);
        }
    }
}
