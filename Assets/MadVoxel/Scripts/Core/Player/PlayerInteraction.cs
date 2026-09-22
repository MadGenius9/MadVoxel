using System;
using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.World.Fields;
using MadVoxel.Inventory;
using MadVoxel.Perks;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Core.Player
{
    public enum TargetKind
    {
        None,
        Block,
        Structure,
        BuildPiece,
        Entity
    }

    public struct InteractionTarget
    {
        public TargetKind Kind;
        public Vector3Int BlockCell;
        public Vector3Int PlaceCell;
        public PlacedStructure Structure;
        public BuildPiece Piece;
        public IDamageable Damageable;
        public IInteractable Interactable;
        public float Distance;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
    }

    /// <summary>
    /// Mine, place, hit and use. One raycast a frame decides what is under the
    /// crosshair; everything else keys off that.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        const float HandMineSpeed = 0.45f;
        const float WrongToolPenalty = 0.35f;
        const float StructureSalvageSeconds = 1.6f;

        GameConfig _config;
        TerrainWorld _voxels;
        StructureWorld _structures;
        BuildingWorld _buildings;
        FieldWorld _fields;
        PlayerInventory _inventory;
        PlayerStats _stats;
        PlayerProgression _progression;
        Camera _camera;

        static readonly PerkEffects NoPerks = new PerkEffects();

        /// <summary>The player's summed perk numbers, or an empty set before Init.</summary>
        PerkEffects Perks { get { return _progression != null ? _progression.Effects : NoPerks; } }

        readonly BuildGhost _ghost = new BuildGhost();

        // Blocks the player put down this session earn no harvest XP when mined again.
        readonly HashSet<Vector3Int> _playerPlaced = new HashSet<Vector3Int>();

        readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        Vector3Int _miningCell;
        float _miningProgress;
        float _salvageProgress;
        PlacedStructure _salvageTarget;
        float _nextAttackTime;
        int _placeRotation;

        public InteractionTarget Target { get; private set; }
        public float MiningProgress01 { get; private set; }

        public event Action<Vector3Int, BlockDefinition> BlockMined;

        public void Init(GameConfig config, TerrainWorld voxels, StructureWorld structures, BuildingWorld buildings,
                         FieldWorld fields, PlayerInventory inventory, PlayerStats stats,
                         PlayerProgression progression, Camera camera)
        {
            _config = config;
            _voxels = voxels;
            _structures = structures;
            _buildings = buildings;
            _fields = fields;
            _inventory = inventory;
            _stats = stats;
            _progression = progression;
            _camera = camera;
            _ghost.EnsureBuilt(null);
        }

        void OnDestroy()
        {
            _ghost.Dispose();
        }

        void Update()
        {
            if (_config == null || _camera == null) return;

            if (!InputBridge.Enabled || _stats == null || !_stats.IsAlive)
            {
                _ghost.Hide();
                ResetMining();
                return;
            }

            Target = Probe();
            UpdateGhost();

            if (InputBridge.RotatePieceDown) _placeRotation = (_placeRotation + 1) % 4;

            if (InputBridge.PrimaryHeld) HandlePrimary();
            else ResetMining();

            if (InputBridge.SecondaryDown) HandleSecondary();
            if (InputBridge.InteractDown) HandleInteract();
        }

        // ------------------------------------------------------------------ probing

        InteractionTarget Probe()
        {
            var result = new InteractionTarget { Kind = TargetKind.None };
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            RaycastHit hit;
            if (!TryRaycastPastSelf(ray, out hit)) return result;

            result.Distance = hit.distance;
            result.HitPoint = hit.point;
            result.HitNormal = hit.normal;

            var piece = hit.collider.GetComponentInParent<BuildPiece>();
            if (piece != null)
            {
                result.Kind = TargetKind.BuildPiece;
                result.Piece = piece;
                result.Damageable = piece;
                result.Interactable = piece.IsOpenable ? piece : null;
                result.HitPoint = hit.point;
                result.HitNormal = hit.normal;
                return result;
            }

            var structure = hit.collider.GetComponentInParent<PlacedStructure>();
            if (structure != null)
            {
                result.Kind = TargetKind.Structure;
                result.Structure = structure;
                result.Damageable = structure;
                result.Interactable = hit.collider.GetComponentInParent<IInteractable>();
                result.BlockCell = structure.Cell;
                result.PlaceCell = Vector3Int.FloorToInt(hit.point + hit.normal * 0.5f);
                return result;
            }

            if (hit.collider.GetComponentInParent<ChunkView>() != null)
            {
                result.Kind = TargetKind.Block;
                result.BlockCell = Vector3Int.FloorToInt(hit.point - hit.normal * 0.02f);
                result.PlaceCell = Vector3Int.FloorToInt(hit.point + hit.normal * 0.02f);
                return result;
            }

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                result.Kind = TargetKind.Entity;
                result.Damageable = damageable;
                result.Interactable = hit.collider.GetComponentInParent<IInteractable>();
            }

            return result;
        }

        /// <summary>
        /// The camera sits inside the player's own capsule, so the nearest hit has to be
        /// filtered rather than trusted.
        /// </summary>
        bool TryRaycastPastSelf(Ray ray, out RaycastHit best)
        {
            best = default(RaycastHit);

            int count = Physics.RaycastNonAlloc(ray, _hitBuffer, _config.reachDistance, ~0, QueryTriggerInteraction.Collide);
            if (count <= 0) return false;

            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var candidate = _hitBuffer[i];
                if (candidate.collider == null) continue;
                if (candidate.collider.transform.IsChildOf(transform)) continue; // ourselves
                if (found && candidate.distance >= best.distance) continue;

                best = candidate;
                found = true;
            }
            return found;
        }

        void UpdateGhost()
        {
            var held = _inventory.SelectedItem;

            if (held != null && held.placeableBuildPiece != null)
            {
                BuildAddress address;
                if (TryResolvePiece(held.placeableBuildPiece, out address))
                {
                    Vector3 centre, size;
                    BuildPlacementSolver.GhostBounds(held.placeableBuildPiece, address, out centre, out size);
                    bool valid = _buildings.CanPlace(held.placeableBuildPiece, address) == BuildingWorld.PlacementResult.Ok;
                    _ghost.ShowBox(centre, size, valid);
                }
                else
                {
                    _ghost.HidePlacement();
                }
            }
            else if (held != null && held.IsPlaceable && Target.Kind != TargetKind.None && Target.Kind != TargetKind.Entity)
            {
                Vector3Int cell = Target.PlaceCell;
                bool valid;
                Vector3Int footprint;

                if (held.placeableStructure != null)
                {
                    footprint = held.placeableStructure.footprint;
                    valid = _structures.CanPlace(held.placeableStructure, cell, _placeRotation) && !IntersectsPlayer(cell, footprint, _placeRotation);
                }
                else
                {
                    footprint = Vector3Int.one;
                    valid = CanPlaceBlockAt(cell);
                }

                _ghost.ShowPlacement(cell, footprint, _placeRotation, valid);
            }
            else
            {
                _ghost.HidePlacement();
            }

            if (Target.Kind == TargetKind.Block) _ghost.ShowHighlight(Target.BlockCell, MiningProgress01);
            else _ghost.HideHighlight();
        }

        bool TryResolvePiece(BuildPieceDefinition def, out BuildAddress address)
        {
            address = default(BuildAddress);
            if (Target.Kind == TargetKind.None) return false;

            return BuildPlacementSolver.TryResolve(_buildings, _voxels, def,
                Target.HitPoint, Target.HitNormal, Target.Piece, out address);
        }

        // ------------------------------------------------------------------- mining

        void HandlePrimary()
        {
            switch (Target.Kind)
            {
                case TargetKind.Block:
                    MineBlock();
                    break;
                case TargetKind.Structure:
                    HandleStructurePrimary();
                    break;
                case TargetKind.BuildPiece:
                    HandleBuildPiecePrimary();
                    break;
                case TargetKind.Entity:
                    Attack(Target.Damageable);
                    ResetMining();
                    break;
                default:
                    ResetMining();
                    break;
            }
        }

        void MineBlock()
        {
            var cell = Target.BlockCell;
            if (cell != _miningCell)
            {
                _miningCell = cell;
                _miningProgress = 0f;
            }

            var def = _voxels.GetBlockDef(cell.x, cell.y, cell.z);
            if (def == null || def.isAir) { ResetMining(); return; }
            if (def.hardness < 0f) { ResetMining(); return; } // indestructible, e.g. bedrock

            var held = _inventory.SelectedStack;
            float speed = MineSpeed(def, held.Item) * Perks.Multiplier(PerkEffectType.MiningSpeedMultiplier);
            if (speed <= 0f)
            {
                MiningProgress01 = 0f;
                Notifications.PostFormat("{0} needs a tier {1} tool", def.displayName, def.requiredToolTier);
                return;
            }

            _miningProgress += speed * Time.deltaTime;
            MiningProgress01 = Mathf.Clamp01(_miningProgress / Mathf.Max(0.05f, def.hardness));

            if (_miningProgress < def.hardness) return;

            BreakBlock(cell, def, held.Item);
            ResetMining();
        }

        static float MineSpeed(BlockDefinition block, ItemDefinition tool)
        {
            int tier = tool != null ? tool.toolTier : 0;
            if (tier < block.requiredToolTier) return 0f;

            if (tool == null || tool.toolType == ToolType.None) return HandMineSpeed;
            if (block.preferredTool == ToolType.None) return tool.harvestSpeed;
            return tool.toolType == block.preferredTool
                ? tool.harvestSpeed
                : tool.harvestSpeed * WrongToolPenalty;
        }

        void BreakBlock(Vector3Int cell, BlockDefinition def, ItemDefinition tool)
        {
            if (!_voxels.SetBlock(cell.x, cell.y, cell.z, _voxels.AirId)) return;

            if (def.dropItem != null)
            {
                int count = UnityEngine.Random.Range(def.dropMin, def.dropMax + 1);
                count = Perks.ScaleCount(PerkEffectType.HarvestYieldMultiplier, count);
                if (count > 0) _inventory.Collect(def.dropItem, count);
            }

            // Forage plants hand back seeds as well as fibre, which is how the garden starts.
            if (def.secondaryDropItem != null && UnityEngine.Random.value <= def.secondaryDropChance)
            {
                int extra = UnityEngine.Random.Range(def.secondaryDropMin, def.secondaryDropMax + 1);
                if (extra > 0) _inventory.Collect(def.secondaryDropItem, extra);
            }

            bool wasPlayerPlaced = _playerPlaced.Remove(cell);
            if (!wasPlayerPlaced && def.harvestXp > 0f)
            {
                _progression.AddXp(def.harvestXp, XpSource.Harvest);
            }

            if (tool != null && tool.HasDurability && _inventory.WearSelected(1))
            {
                Notifications.PostFormat("{0} broke", tool.displayName);
            }

            if (BlockMined != null) BlockMined(cell, def);
        }

        void ResetMining()
        {
            _miningProgress = 0f;
            MiningProgress01 = 0f;
            _salvageProgress = 0f;
            _salvageTarget = null;
        }

        // --------------------------------------------------------------- structures

        void HandleStructurePrimary()
        {
            var structure = Target.Structure;
            var held = _inventory.SelectedItem;

            if (held != null && held.toolType == ToolType.Wrench)
            {
                if (_salvageTarget != structure)
                {
                    _salvageTarget = structure;
                    _salvageProgress = 0f;
                }

                _salvageProgress += Time.deltaTime;
                MiningProgress01 = Mathf.Clamp01(_salvageProgress / StructureSalvageSeconds);
                if (_salvageProgress < StructureSalvageSeconds) return;

                var def = structure.Definition;
                if (def.salvageItem != null)
                {
                    _inventory.Collect(def.salvageItem,
                        Perks.ScaleCount(PerkEffectType.LootQuantityMultiplier, def.salvageCount));
                }
                _structures.Destroy(structure, true);
                ResetMining();
                return;
            }

            Attack(structure);
        }

        /// <summary>Hammer repairs a snap piece; anything else just hits it.</summary>
        void HandleBuildPiecePrimary()
        {
            var held = _inventory.SelectedItem;
            var piece = Target.Piece;
            if (piece == null) return;

            if (held != null && held.toolType == ToolType.Hammer)
            {
                if (Time.time < _nextAttackTime) return;
                _nextAttackTime = Time.time + 0.35f;

                if (piece.HealthFraction >= 1f)
                {
                    Notifications.PostFormat("{0} is undamaged", piece.Definition.displayName);
                    return;
                }
                piece.Repair(piece.Definition.maxHealth * 0.2f
                             * Perks.Multiplier(PerkEffectType.RepairSpeedMultiplier));
                Notifications.PostFormat("Repaired {0} ({1}%)", piece.Definition.displayName,
                    Mathf.RoundToInt(piece.HealthFraction * 100f));
                return;
            }

            Attack(piece);
            ResetMining();
        }

        void Attack(IDamageable damageable)
        {
            if (damageable == null || Time.time < _nextAttackTime) return;

            var held = _inventory.SelectedStack;
            float damage = held.Item != null && held.Item.meleeDamage > 0f ? held.Item.meleeDamage : 4f;
            damage *= Perks.Multiplier(PerkEffectType.MeleeDamageMultiplier);
            float cooldown = held.Item != null ? Mathf.Max(0.2f, held.Item.attackCooldown) : 0.6f;
            _nextAttackTime = Time.time + cooldown;

            if (!_stats.TrySpendStamina(3f)) return;

            damageable.ApplyDamage(new DamageInfo
            {
                Amount = damage,
                Kind = DamageKind.Melee,
                Point = transform.position,
                Direction = _camera.transform.forward,
                Source = gameObject,
                ToolTier = held.Item != null ? held.Item.toolTier : 0
            });

            if (held.Item != null && held.Item.HasDurability && _inventory.WearSelected(1))
            {
                Notifications.PostFormat("{0} broke", held.Item.displayName);
            }
        }

        // ------------------------------------------------------------------ placing

        void HandleSecondary()
        {
            var held = _inventory.SelectedStack;
            if (held.IsEmpty) return;

            if (held.Item.category == ItemCategory.Consumable && (held.Item.foodRestore > 0f || held.Item.waterRestore > 0f || held.Item.healthRestore > 0f || held.Item.staminaRestore > 0f))
            {
                _stats.Consume(held.Item.foodRestore, held.Item.waterRestore, held.Item.healthRestore, held.Item.staminaRestore);
                _inventory.ConsumeSelected(1);
                Notifications.PostFormat("Ate {0}", held.Item.displayName);
                return;
            }

            if (held.Item.toolType == ToolType.Hammer && Target.Kind == TargetKind.BuildPiece)
            {
                UpgradePiece(Target.Piece);
                return;
            }

            // The hoe breaks open ground into a field cell. One cell by hand today; a
            // Phase 1 implement will work a swath through the same call.
            if (held.Item.stringId == MadVoxel.Content.ItemIds.Hoe && Target.Kind == TargetKind.Block)
            {
                PlowTarget();
                return;
            }

            if (Target.Kind == TargetKind.None) return;

            if (held.Item.placeableBuildPiece != null) PlaceBuildPiece(held.Item);
            else if (held.Item.placeableStructure != null) PlaceStructure(held.Item);
            else if (held.Item.placeableBlock != null) PlaceBlock(held.Item);
        }

        void PlowTarget()
        {
            if (_fields == null) return;
            if (!_stats.TrySpendStamina(6f))
            {
                Notifications.Post("Too tired to break ground");
                return;
            }

            if (!_fields.TryPlow(Target.BlockCell))
            {
                Notifications.Post("Nothing to plow here");
                return;
            }

            _inventory.WearSelected(1);
            _progression.AddXp(2f, XpSource.Harvest);
            Notifications.Post("Ground broken");
        }

        void PlaceBuildPiece(ItemDefinition item)
        {
            var def = item.placeableBuildPiece;

            BuildAddress address;
            if (!TryResolvePiece(def, out address))
            {
                Notifications.Post("No surface to snap to");
                return;
            }

            var check = _buildings.CanPlace(def, address);
            if (check != BuildingWorld.PlacementResult.Ok)
            {
                Notifications.Post(BuildingWorld.Describe(check));
                return;
            }

            if (_buildings.Place(def, address) == null) return;
            _inventory.ConsumeSelected(1);
        }

        void UpgradePiece(BuildPiece piece)
        {
            if (piece == null) return;

            var result = _buildings.Upgrade(piece, _inventory.Bag, _progression.GetRank);
            if (result != BuildingWorld.UpgradeResult.Ok)
            {
                Notifications.Post(BuildingWorld.Describe(result, piece));
                return;
            }

            // Upgrading is real work on your own base, so it pays.
            _progression.AddXp(12f, XpSource.Build);
            Notifications.PostFormat("Upgraded to {0}", piece.Definition.displayName);
        }

        void PlaceBlock(ItemDefinition item)
        {
            var cell = Target.PlaceCell;
            if (!CanPlaceBlockAt(cell))
            {
                Notifications.Post("Blocked");
                return;
            }

            if (!_voxels.SetBlock(cell.x, cell.y, cell.z, item.placeableBlock.RuntimeId)) return;
            _playerPlaced.Add(cell);
            _inventory.ConsumeSelected(1);
        }

        bool CanPlaceBlockAt(Vector3Int cell)
        {
            if (!TerrainWorld.InVerticalRange(cell.y)) return false;
            if (!_voxels.Registry.IsAir(_voxels.GetBlock(cell.x, cell.y, cell.z))) return false;
            if (_structures.IsOccupied(cell)) return false;
            return !IntersectsPlayer(cell, Vector3Int.one, 0);
        }

        void PlaceStructure(ItemDefinition item)
        {
            var def = item.placeableStructure;
            var cell = Target.PlaceCell;

            if (IntersectsPlayer(cell, def.footprint, _placeRotation))
            {
                Notifications.Post("Not enough room");
                return;
            }

            var placed = _structures.Place(def, cell, _placeRotation);
            if (placed == null)
            {
                Notifications.Post("Needs support");
                return;
            }

            _inventory.ConsumeSelected(1);
            if (def.kind == StructureKind.ToolCupboard)
            {
                Notifications.PostFormat("Land claimed - {0}m protected", Mathf.RoundToInt(placed.Definition.claimRadius > 0f ? placed.Definition.claimRadius : _config.claimRadius));
            }
        }

        bool IntersectsPlayer(Vector3Int cell, Vector3Int footprint, int rotationSteps)
        {
            var size = PlacedStructure.RotatedFootprint(footprint, rotationSteps);
            var min = new Vector3(cell.x, cell.y, cell.z);
            var max = min + new Vector3(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z));
            var bounds = new Bounds((min + max) * 0.5f, max - min);

            var controller = GetComponent<CharacterController>();
            float radius = controller != null ? controller.radius + 0.05f : 0.4f;
            float height = controller != null ? controller.height : 1.8f;

            var playerBounds = new Bounds(
                transform.position + Vector3.up * (height * 0.5f),
                new Vector3(radius * 2f, height, radius * 2f));

            return bounds.Intersects(playerBounds);
        }

        // ---------------------------------------------------------------- interact

        void HandleInteract()
        {
            if (Target.Interactable == null) return;
            Target.Interactable.Interact(gameObject);
        }

        /// <summary>Marks blocks restored by the save layer so they do not farm XP.</summary>
        public void MarkPlayerPlaced(Vector3Int cell)
        {
            _playerPlaced.Add(cell);
        }

        public string TargetPrompt
        {
            get
            {
                if (Target.Interactable != null && !string.IsNullOrEmpty(Target.Interactable.InteractPrompt))
                    return "[E] " + Target.Interactable.InteractPrompt;

                if (Target.Kind == TargetKind.BuildPiece && Target.Piece != null)
                {
                    var held = _inventory.SelectedItem;
                    string name = string.Format("{0} ({1})", Target.Piece.Definition.displayName, Target.Piece.Tier);
                    if (held != null && held.toolType == ToolType.Hammer)
                    {
                        return Target.Piece.Definition.IsTopTier
                            ? name + "  -  LMB repair"
                            : name + "  -  RMB upgrade, LMB repair";
                    }
                    return name;
                }
                if (Target.Kind == TargetKind.Block)
                {
                    var def = _voxels.GetBlockDef(Target.BlockCell.x, Target.BlockCell.y, Target.BlockCell.z);
                    if (def == null || def.isAir) return "";

                    string field = _fields != null ? _fields.DescribeAt(Target.BlockCell) : "";
                    if (!string.IsNullOrEmpty(field)) return def.displayName + "  -  " + field;

                    var held = _inventory.SelectedItem;
                    if (held != null && held.stringId == MadVoxel.Content.ItemIds.Hoe && _fields != null && _fields.IsTillable(Target.BlockCell))
                        return def.displayName + "  -  RMB plow";

                    return def.displayName;
                }
                return "";
            }
        }
    }
}
