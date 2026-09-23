using System;

namespace MadVoxel.World.Fields
{
    /// <summary>
    /// The FS-style tillage cycle. A cell walks Wild → Plowed → Cultivated → Seeded →
    /// Growing → Ready → Stubble, and stubble goes back round by being plowed again.
    /// </summary>
    public enum FieldCellState : byte
    {
        Wild,
        Plowed,
        Cultivated,
        Seeded,
        Growing,
        Ready,
        Stubble
    }

    /// <summary>
    /// One square metre of field. Kept as a small struct in a flat dictionary because a
    /// worked field is tens of thousands of these and they are read far more often than
    /// they are written.
    /// </summary>
    [Serializable]
    public struct FieldCell
    {
        public FieldCellState State;
        /// <summary>Index into the field crop table. 0 means nothing sown.</summary>
        public byte CropIndex;
        /// <summary>0..1. Scales the litres a cell yields; fertiliser is not built yet.</summary>
        public float Moisture;
        /// <summary>0..1.</summary>
        public float Fertiliser;
        /// <summary>Multiplier on the litres this cell yields. 1 is a normal cell.</summary>
        public float YieldFactor;
        /// <summary>World-clock hour the cell last changed state, for growth timing.</summary>
        public double ChangedAtHours;

        public static FieldCell Wild
        {
            get
            {
                return new FieldCell
                {
                    State = FieldCellState.Wild,
                    CropIndex = 0,
                    Moisture = 0.5f,
                    Fertiliser = 0.25f,
                    YieldFactor = 1f,
                    ChangedAtHours = 0.0
                };
            }
        }

        public bool IsWorkable
        {
            get { return State != FieldCellState.Wild; }
        }

        public bool HasCrop
        {
            get
            {
                return CropIndex != 0 &&
                       (State == FieldCellState.Seeded || State == FieldCellState.Growing || State == FieldCellState.Ready);
            }
        }
    }
}
