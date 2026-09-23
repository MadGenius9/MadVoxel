using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The tractor gauge cluster. Built once and switched off until the player gets on
    /// a machine; <see cref="MadVoxel.Vehicles.VehicleWorld"/> calls <see cref="Mount"/>
    /// and feeds <see cref="SetReadout"/> every frame while they are driving.
    ///
    /// It was built before anything drove, deliberately: the HUD is mode-aware by
    /// construction, and a mode with no layer behind it is a mode that quietly rots.
    /// When the tractor landed it needed no HUD surgery and no second layout pass.
    ///
    /// Claim Slate rules still bind here. No lime FS25 widgets: the cluster is the same
    /// oil-black plate with sodium-gold needles, and it never appears on foot.
    /// </summary>
    public class TractorPanel : MonoBehaviour
    {
        const float Width = 380f;
        const float Height = 150f;

        RectTransform _root;
        Text _speed, _implement;
        ClaimSlate.Gauge _fuel, _hopper;
        Image _implementLamp;

        /// <summary>True only while the player is actually on a machine.</summary>
        public bool IsDriving { get; private set; }

        public void Build(Transform parent)
        {
            var plate = ClaimSlate.Fill(parent, "TractorPanel", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.78f));
            ClaimSlate.Place(plate.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-30f, 30f), new Vector2(Width, Height));
            _root = plate.rectTransform;

            ClaimSlate.Frame(_root, ClaimSlate.Dim(ClaimSlate.Bone, 0.20f), 1f);
            ClaimSlate.Rivets(_root, 11f, 4f);

            var rule = ClaimSlate.Fill(_root, "Rule", ClaimSlate.OxideRust);
            ClaimSlate.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -12f), new Vector2(Width - 28f, 2f));

            // Speed is the big number. A needle dial would be icon soup.
            _speed = ClaimSlate.Stencil(_root, "Speed", "0", 44, TextAnchor.UpperLeft, ClaimSlate.SodiumGold);
            ClaimSlate.Place(ClaimSlate.Holder(_speed), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -20f), new Vector2(150f, 48f));

            var unit = ClaimSlate.Stencil(_root, "Unit", "KM/H", 15, TextAnchor.UpperLeft, ClaimSlate.BoneDim);
            ClaimSlate.Place(ClaimSlate.Holder(unit), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -66f), new Vector2(150f, 18f));

            _fuel = ClaimSlate.CreateGauge(_root, "Fuel", ClaimSlate.SodiumGold, ClaimSlate.Tick.Drop, 190f);
            ClaimSlate.Place(_fuel.Root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -24f), new Vector2(190f, 18f));

            _hopper = ClaimSlate.CreateGauge(_root, "Hopper", ClaimSlate.CropSage, ClaimSlate.Tick.Grain, 190f);
            ClaimSlate.Place(_hopper.Root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -50f), new Vector2(190f, 18f));

            // Implement state is a lamp plus a word: shape and colour, same as the vitals.
            _implementLamp = ClaimSlate.Fill(_root, "ImplementLamp", ClaimSlate.Dim(ClaimSlate.CropSage, 0.3f));
            ClaimSlate.Place(_implementLamp.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 20f), new Vector2(10f, 10f));

            _implement = ClaimSlate.Stencil(_root, "Implement", "NO IMPLEMENT", 17, TextAnchor.LowerLeft, ClaimSlate.BoneDim);
            ClaimSlate.Place(ClaimSlate.Holder(_implement), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(34f, 14f), new Vector2(Width - 50f, 20f));

            SetReadout(0f, 0f, 1f, 0f, 1f, "NO IMPLEMENT", false);
            _root.gameObject.SetActive(false);
        }

        /// <summary>The player got on a machine.</summary>
        public void Mount()
        {
            IsDriving = true;
            if (_root != null) _root.gameObject.SetActive(true);
        }

        public void Dismount()
        {
            IsDriving = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>Litres, not percentages: the field layer counts in litres everywhere.</summary>
        public void SetReadout(float speedKph, float fuelLitres, float fuelCapacity,
                               float hopperLitres, float hopperCapacity,
                               string implementName, bool implementEngaged)
        {
            if (_root == null) return;

            _speed.text = Mathf.RoundToInt(Mathf.Max(0f, speedKph)).ToString();

            _fuel.Set(fuelLitres, fuelCapacity,
                string.Format("{0} L", Mathf.RoundToInt(fuelLitres)),
                fuelLitres / Mathf.Max(0.01f, fuelCapacity) <= 0.15f ? ClaimSlate.OxideRust : ClaimSlate.SodiumGold);

            _hopper.Set(hopperLitres, hopperCapacity,
                string.Format("{0} L", Mathf.RoundToInt(hopperLitres)), ClaimSlate.CropSage);

            _implement.text = string.IsNullOrEmpty(implementName) ? "NO IMPLEMENT" : implementName.ToUpperInvariant();
            _implement.color = implementEngaged ? ClaimSlate.Bone : ClaimSlate.BoneDim;
            _implementLamp.color = implementEngaged ? ClaimSlate.CropSage : ClaimSlate.Dim(ClaimSlate.CropSage, 0.3f);
        }
    }
}
