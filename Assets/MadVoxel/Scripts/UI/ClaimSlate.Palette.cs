using UnityEngine;

namespace MadVoxel.UI
{
    /// <summary>
    /// Claim Slate's colours and the pure decisions made from them. Split out from the
    /// widget builders on purpose: this half touches nothing but <see cref="Color"/>, so
    /// the headless checks can compile it and hold the palette to the spec.
    /// </summary>
    public static partial class ClaimSlate
    {
        // ------------------------------------------------------------------ palette

        public static readonly Color OilBlack = Hex(0x0C0B0A);
        public static readonly Color Bone = Hex(0xE6E0D6);
        public static readonly Color OxideRust = Hex(0xB85A32);
        public static readonly Color SodiumGold = Hex(0xD4A017);
        public static readonly Color CropSage = Hex(0x6B7A4A);

        /// <summary>Reserved. Critical health and the blood moon, nothing else.</summary>
        public static readonly Color Blood = Hex(0x8B1E1E);

        // Derived tones. Kept here so no screen invents its own grey.
        public static readonly Color Slate = new Color(0.078f, 0.071f, 0.063f, 0.93f);
        public static readonly Color SlateDeep = new Color(0.047f, 0.043f, 0.039f, 0.985f);
        public static readonly Color SlateSoft = new Color(0.106f, 0.098f, 0.086f, 0.90f);
        public static readonly Color Metal = new Color(0.137f, 0.129f, 0.118f, 0.88f);
        public static readonly Color MetalLit = new Color(0.196f, 0.180f, 0.157f, 0.94f);

        public static readonly Color BoneDim = Dim(Bone, 0.58f);
        public static readonly Color BoneFaint = Dim(Bone, 0.30f);
        public static readonly Color Rivet = Dim(Bone, 0.40f);
        public static readonly Color Scratch = new Color(0.90f, 0.88f, 0.84f, 0.05f);

        /// <summary>Grease pencil: what a thing would be if you had bought it yet.</summary>
        public static readonly Color Pencil = new Color(0.62f, 0.58f, 0.52f, 0.55f);

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        public static Color Dim(Color c, float amount)
        {
            return new Color(c.r * amount, c.g * amount, c.b * amount, c.a);
        }

        public static Color Fade(Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, alpha);
        }

        /// <summary>Health colour by fraction. Bone until it matters, then blood.</summary>
        public static Color VitalColour(float fraction01)
        {
            if (fraction01 <= 0.25f) return Blood;
            if (fraction01 <= 0.5f) return OxideRust;
            return Bone;
        }
    }
}
