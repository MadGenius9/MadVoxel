using MadVoxel.Building;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Colony
{
    /// <summary>
    /// The charter, nailed to a wall. It is the only colony screen there is: no second
    /// HUD, no overlay, no roster panel floating over the world. If you want to know
    /// how the colony is doing you walk to the board and read it, which is the same
    /// deal the tool cupboard already makes for building privilege.
    /// </summary>
    public class ColonyBoardStructure : MonoBehaviour, IInteractable
    {
        /// <summary>Raised when a player uses the board, so the UI can open it.</summary>
        public static event System.Action<ColonyBoardStructure> OpenRequested;

        public PlacedStructure Structure { get; private set; }

        /// <summary>Set by the session once the colony exists.</summary>
        public ColonyWorld Colony { get; set; }

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
        }

        public string InteractPrompt
        {
            get
            {
                if (Colony == null) return "Colony Board";
                if (!Colony.Founded) return "Colony Board  [E] found a colony";

                return string.Format("{0}  -  {1} of {2}  [E] open",
                    Colony.ColonyName, Colony.Population,
                    Colony.Rules != null ? Colony.Rules.maxColonists : 3);
            }
        }

        public void Interact(GameObject interactor)
        {
            if (Colony == null)
            {
                Notifications.Post("The board is not connected to anything yet");
                return;
            }

            if (OpenRequested != null) OpenRequested(this);
        }

        /// <summary>The look-at line, for the visor.</summary>
        public string Readout()
        {
            if (Colony == null) return "COLONY BOARD";
            if (!Colony.Founded) return "COLONY BOARD   UNFOUNDED";

            int population = Colony.Population;
            float foodDays = ColonyCharter.FoodDays(Colony.FoodInStore(), population, Colony.Rules);
            float waterDays = ColonyCharter.WaterDays(Colony.WaterInStore(), population, Colony.Rules);

            return string.Format("{0}   POP {1}   FOOD {2}   WATER {3}",
                Colony.ColonyName.ToUpperInvariant(), population,
                ColonyCharter.DescribeDays(foodDays), ColonyCharter.DescribeDays(waterDays));
        }
    }
}
