using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Farming.Crops;
using MadVoxel.Perks;
using MadVoxel.World.Biomes;
using UnityEngine;

namespace MadVoxel.Farming.Plots
{
    /// <summary>
    /// A garden farm plot: a framed soil bed you place on dirt, plant a seed in, and
    /// harvest by hand. Growth runs on the world clock from the hour it was planted, so
    /// a crop keeps maturing across a save and reload rather than only while you watch.
    ///
    /// This is the survival garden that feeds you tonight. The field layer
    /// (<see cref="MadVoxel.World.Fields"/>) is the bulk one, and is Phase 1.
    /// </summary>
    public class FarmPlotStructure : MonoBehaviour, IInteractable
    {
        public PlacedStructure Structure { get; private set; }
        public CropDefinition Crop { get; private set; }
        /// <summary>World-clock hour the current growth started. Survives save and reload.</summary>
        public double PlantedAtHours { get; private set; }

        FarmPlotVisuals _visuals;
        CropStage _shownStage = CropStage.Empty;
        float _tick;

        public bool IsEmpty { get { return Crop == null; } }

        /// <summary>
        /// Growth is derived from the planting hour rather than ticked, so it survives a
        /// save and a reload untouched. The biome scales the elapsed hours rather than
        /// the crop's maturity time, which keeps that property: the same plot, planted
        /// at the same hour, always reads the same stage on the same ground.
        /// </summary>
        public double HoursGrown
        {
            get
            {
                var clock = Structure != null && Structure.Owner != null ? Structure.Owner.Clock : null;
                if (clock == null || Crop == null) return 0.0;

                double raw = System.Math.Max(0.0, clock.TotalHours - PlantedAtHours);
                return raw * GrowthRate;
            }
        }

        /// <summary>How fast this ground brings a crop on. 1 is farmland.</summary>
        public float GrowthRate
        {
            get
            {
                var owner = Structure != null ? Structure.Owner : null;
                if (owner == null || owner.Voxels == null || owner.Content == null || owner.Content.biomes == null) return 1f;

                var def = owner.Content.biomes.Find(owner.Voxels.BiomeAt(Structure.Cell.x, Structure.Cell.z));
                return def != null ? Mathf.Max(0.05f, def.plotGrowthMultiplier) : 1f;
            }
        }

        public CropStage Stage
        {
            get { return Crop == null ? CropStage.Empty : Crop.StageAt(HoursGrown); }
        }

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
            _visuals = new FarmPlotVisuals(structure.transform);
            RefreshVisuals(true);
        }

        void Update()
        {
            // Growth is a clock difference, so it only has to be sampled often enough
            // for the plant to visibly change.
            _tick += Time.deltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            RefreshVisuals(false);
        }

        void RefreshVisuals(bool force)
        {
            var stage = Stage;
            if (!force && stage == _shownStage) return;

            _shownStage = stage;
            _visuals.Show(Crop, stage, Crop != null ? Crop.Progress01(HoursGrown) : 0f);
        }

        // ------------------------------------------------------------------ state

        public void Plant(CropDefinition crop, double atHours)
        {
            Crop = crop;
            PlantedAtHours = atHours;
            RefreshVisuals(true);
        }

        /// <summary>
        /// Frost taking a growth stage back. Growth is derived from the planting hour,
        /// so "losing a stage" means pushing that hour forward - which keeps the whole
        /// derived-growth property intact through a save and a reload.
        /// </summary>
        public void SetBackAStage()
        {
            if (Crop == null) return;

            var clock = Structure != null && Structure.Owner != null ? Structure.Owner.Clock : null;
            if (clock == null) return;

            double stageHours = Crop.HoursToMature / 4.0;
            PlantedAtHours = System.Math.Min(clock.TotalHours, PlantedAtHours + stageHours);

            RefreshVisuals(true);
            Notifications.PostFormat("Frost set back the {0}", Crop.displayName.ToLowerInvariant());
        }

        public void ClearCrop()
        {
            Crop = null;
            PlantedAtHours = 0.0;
            RefreshVisuals(true);
        }

        /// <summary>Used by the save layer to restore a crop mid-growth.</summary>
        public void RestoreCrop(CropDefinition crop, double plantedAtHours)
        {
            Crop = crop;
            PlantedAtHours = plantedAtHours;
            RefreshVisuals(true);
        }

        // ------------------------------------------------------------ interaction

        public string InteractPrompt
        {
            get
            {
                if (Crop == null) return "Plant a seed";

                var stage = Stage;
                if (stage == CropStage.Ready) return "Harvest " + Crop.displayName;

                float remaining = Crop.HoursToMature - (float)HoursGrown;
                return string.Format("{0} - {1} ({2}h left)", Crop.displayName, stage, Mathf.CeilToInt(Mathf.Max(0f, remaining)));
            }
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;

            var inventory = interactor.GetComponent<PlayerInventory>();
            var progression = interactor.GetComponent<PlayerProgression>();
            if (inventory == null) return;

            if (Crop == null) TryPlantFromHand(inventory, progression);
            else if (Stage == CropStage.Ready) Harvest(inventory, progression);
            else Notifications.Post(InteractPrompt);
        }

        void TryPlantFromHand(PlayerInventory inventory, PlayerProgression progression)
        {
            var held = inventory.SelectedItem;
            var content = Structure.Owner != null ? Structure.Owner.Content : null;
            if (content == null) return;

            var crop = held != null ? content.CropForSeed(held) : null;
            if (crop == null)
            {
                Notifications.Post("Hold a seed to plant it here");
                return;
            }

            if (!crop.growsOnPlot)
            {
                Notifications.PostFormat("{0} will not grow in a plot", crop.displayName);
                return;
            }

            // Some ground simply refuses a crop. Saying so at planting time is kinder
            // than letting it sit in the bed for two days and yield nothing.
            var here = Biome;
            if (crop.IsBlockedIn(here))
            {
                Notifications.PostFormat("{0} will not take in {1}", crop.displayName, BiomeIds.DisplayName(here));
                return;
            }

            var clock = Structure.Owner.Clock;
            Plant(crop, clock != null ? clock.TotalHours : 0.0);
            inventory.ConsumeSelected(1);

            if (progression != null) progression.AddXp(3f, XpSource.Harvest);
            Notifications.PostFormat("Planted {0}", crop.displayName);
        }

        void Harvest(PlayerInventory inventory, PlayerProgression progression)
        {
            var crop = Crop;
            if (crop == null || crop.harvestItem == null) return;

            int yield = Random.Range(crop.harvestMin, crop.harvestMax + 1);

            // Where the plot stands matters as much as what is in it: the biome's own
            // multiplier and the crop's opinion of that biome stack, so wheat on the dry
            // flats is poor twice over.
            float ground = GroundMultiplier(crop);
            if (ground < 0.999f || ground > 1.001f)
            {
                yield = Mathf.Max(ground > 0f ? 1 : 0, Mathf.RoundToInt(yield * ground));
            }

            // A sprinkler keeping this bed wet pays for its copper here - and a bed with
            // nothing on it pays for the drought.
            yield = Mathf.RoundToInt(yield * (1f + WaterBonus));
            float dry = DryLoss(progression);
            if (dry > 0f) yield = Mathf.Max(1, Mathf.RoundToInt(yield * (1f - dry)));

            // The Farming perk is what makes a garden worth expanding.
            if (progression != null)
            {
                yield = progression.Effects.ScaleCount(PerkEffectType.HarvestYieldMultiplier, yield);
            }

            if (yield > 0) inventory.Collect(crop.harvestItem, yield);

            if (crop.replants)
            {
                // Perennials keep the bed: harvesting just restarts the clock.
                var clock = Structure.Owner.Clock;
                Plant(crop, clock != null ? clock.TotalHours : 0.0);
                Notifications.PostFormat("Harvested {0} - the plant keeps growing", crop.displayName);
            }
            else
            {
                if (crop.seedItem != null && Random.value <= SeedReturnChance(progression))
                {
                    int seeds = Random.Range(crop.seedReturnMin, crop.seedReturnMax + 1);
                    if (seeds > 0) inventory.Collect(crop.seedItem, seeds);
                }
                ClearCrop();
                Notifications.PostFormat("Harvested {0} - replant the plot", crop.displayName);
            }

            if (progression != null) progression.AddXp(crop.xpPerHarvest, XpSource.Harvest);
        }

        /// <summary>
        /// Set by a sprinkler that is reaching this bed, and by the weather when a
        /// drought is biting. Zero is "nobody has watered this and nothing is wrong".
        /// </summary>
        public float WaterBonus { get; set; }

        /// <summary>
        /// Set by the weather while a drought is biting. A sprinkler cancels it
        /// outright, and the Farming perk takes the edge off what is left - which is
        /// what makes a tank and a line the answer rather than a prayer.
        /// </summary>
        public float DroughtLoss { get; set; }

        float DryLoss(PlayerProgression progression)
        {
            if (DroughtLoss <= 0f || WaterBonus > 0f) return 0f;

            float resist = progression != null
                ? progression.Effects.Bonus(PerkEffectType.DroughtResistance) : 0f;

            return Mathf.Clamp01(DroughtLoss * Mathf.Clamp01(1f - resist));
        }

        /// <summary>The biome this plot stands on, and the crop's opinion of it.</summary>
        public BiomeId Biome
        {
            get
            {
                var owner = Structure != null ? Structure.Owner : null;
                var voxels = owner != null ? owner.Voxels : null;
                return voxels != null ? voxels.BiomeAt(Structure.Cell.x, Structure.Cell.z) : BiomeId.Farmland;
            }
        }

        float GroundMultiplier(CropDefinition crop)
        {
            var owner = Structure != null ? Structure.Owner : null;
            var voxels = owner != null ? owner.Voxels : null;
            if (voxels == null || owner.Content == null || owner.Content.biomes == null) return 1f;

            var biome = voxels.BiomeAt(Structure.Cell.x, Structure.Cell.z);
            var def = owner.Content.biomes.Find(biome);

            float blanket = def != null ? def.cropYieldMultiplier : 1f;
            return Mathf.Max(0f, blanket * crop.YieldIn(biome));
        }

        float SeedReturnChance(PlayerProgression progression)
        {
            float chance = Crop.seedReturnChance;
            if (progression == null) return chance;

            int rank = progression.GetRank(PerkIds.Farming);
            return Mathf.Clamp01(chance + 0.08f * rank);
        }
    }
}
