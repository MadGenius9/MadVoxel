using System;
using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.World.Fields;
using MadVoxel.Inventory;
using MadVoxel.Fluid;
using MadVoxel.Perks;
using MadVoxel.Power;
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
        Entity,
        /// <summary>
        /// Something you use but cannot fight: a trader's counter. Without this a
        /// fixture that is not <see cref="IDamageable"/> is invisible to the probe,
        /// because every earlier branch is looking for something with health.
        /// </summary>
        Fixture
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
    /// <summary>The snap-placement state, published for the HUD's building layer.</summary>
    public struct BuildReadout
    {
        /// <summary>True while a snap piece is in hand, whether or not it can be placed.</summary>
        public bool Active;
        public bool Resolved;
        public bool Valid;
        public string PieceName;
        public string Tier;
        /// <summary>Why it will not go there, when it will not.</summary>
        public string Reason;
    }

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

        /// <summary>
        /// Wiring and plumbing. One tool does both graphs, because from the player's
        /// side it is the same gesture: click a fitting, click the next one.
        /// </summary>
        public WireTool Wiring { get; private set; }

        PowerWorld _power;
        FluidWorld _fluid;

        // Blocks the player put down earn no harvest XP when mined again. Saved, so
        // the guard survives a reload - it used to be session-only, which made
        // "place a block, quit, reload, mine it" a working XP mill.
        readonly HashSet<Vector3Int> _playerPlaced = new HashSet<Vector3Int>();

        /// <summary>The cells the player laid, for the save layer.</summary>
        public IEnumerable<Vector3Int> PlayerPlacedCells { get { return _playerPlaced; } }

        /// <summary>Replaces the remembered cells on load.</summary>
        public void RestorePlayerPlaced(IEnumerable<Vector3Int> cells)
        {
            _playerPlaced.Clear();
            if (cells == null) return;
            foreach (var cell in cells) _playerPlaced.Add(cell);
        }

        /// <summary>
        /// Forgets a cell whoever emptied it.
        ///
        /// The set used to be pruned only where the player mined, which was harmless
        /// while it lived for one session. Now that it is saved, a zombie chewing
        /// through a wall - or a plough turning a block into tilled soil - would leave
        /// an entry behind that is written to disk on every autosave from then on, and
        /// the set would grow for as long as the world does rather than staying
        /// bounded by what is standing.
        /// </summary>
        void OnBlockChanged(Vector3Int cell, ushort oldId, ushort newId)
        {
            if (_playerPlaced.Count == 0) return;
            if (_voxels != null && _voxels.Registry.IsAir(oldId)) return;

            _playerPlaced.Remove(cell);
        }

        readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        // Grown on demand. A blood moon can put a lot of bodies inside one swing.
        Combat.MeleeArc.Candidate[] _swingBuffer = new Combat.MeleeArc.Candidate[16];

        /// <summary>Metres to the body the last sweep chose, for beating the crosshair.</summary>
        float _swingDistance;

        /// <summary>Seconds of empty-handed cooldown, so a bare fist is not a machine gun.</summary>
        const float BareHandCooldown = 0.6f;

        /// <summary>Rate limit on the winded message, which would otherwise arrive every frame.</summary>
        float _nextWindedMessage;

        Vector3Int _miningCell;
        float _miningProgress;
        float _salvageProgress;
        PlacedStructure _salvageTarget;
        float _nextAttackTime;
        int _placeRotation;

        public InteractionTarget Target { get; private set; }
        public float MiningProgress01 { get; private set; }

        /// <summary>
        /// What the snap ghost is currently saying. The HUD reads this instead of
        /// re-running the placement solver, so the readout and the ghost can never
        /// disagree with each other.
        /// </summary>
        public BuildReadout Build { get; private set; }

        public event Action<Vector3Int, BlockDefinition> BlockMined;

        /// <summary>
        /// Set after construction: the machine yard is built once the player and the HUD
        /// both exist, which is after this component is created.
        /// </summary>
        public Vehicles.VehicleWorld Vehicles { get; set; }

        /// <summary>
        /// Who is alive to be swung at. Set by the session once the spawner exists;
        /// melee simply finds nothing until then rather than needing to care.
        /// </summary>
        public AI.SpawnDirector Spawns { get; set; }

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

            // Whoever removes a block, the "I put this here" guard forgets it.
            _voxels.BlockChanged += OnBlockChanged;
        }

        void OnDestroy()
        {
            if (_voxels != null) _voxels.BlockChanged -= OnBlockChanged;
            _ghost.Dispose();
        }

        void Update()
        {
            if (_config == null || _camera == null) return;

            if (!InputBridge.Enabled || _stats == null || !_stats.IsAlive)
            {
                _ghost.Hide();
                ResetMining();

                // Let the string down rather than holding it. Opening the inventory
                // mid-draw and closing it again used to loose an arrow the player never
                // released, and the draw bar stayed lit over the menu.
                _drawHeld = 0f;
                Draw01 = 0f;
                return;
            }

            Target = Probe();
            UpdateGhost();

            if (InputBridge.RotatePieceDown) _placeRotation = (_placeRotation + 1) % 4;

            // A bow takes the primary button - you draw on hold and loose on release,
            // with no swing underneath to fall through to. It takes only that button,
            // though: a bow in your hand must not stop you opening a door.
            bool drawing = HandleBow();

            if (!drawing)
            {
                if (InputBridge.PrimaryHeld) HandlePrimary();
                else ResetMining();
            }

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
                return result;
            }

            // Last: something you can use but not fight. A trader's counter has no
            // health, so every branch above walks past it.
            var fixture = hit.collider.GetComponentInParent<IInteractable>();
            if (fixture != null)
            {
                result.Kind = TargetKind.Fixture;
                result.Interactable = fixture;
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
                var piece = held.placeableBuildPiece;
                var readout = new BuildReadout
                {
                    Active = true,
                    PieceName = piece.displayName,
                    Tier = piece.tier.ToString()
                };

                BuildAddress address;
                if (TryResolvePiece(piece, out address))
                {
                    Vector3 centre, size;
                    BuildPlacementSolver.GhostBounds(piece, address, out centre, out size);

                    var result = _buildings.CanPlace(piece, address);
                    bool valid = result == BuildingWorld.PlacementResult.Ok;
                    _ghost.ShowBox(centre, size, valid);

                    readout.Resolved = true;
                    readout.Valid = valid;
                    readout.Reason = valid ? "" : BuildingWorld.Describe(result);
                }
                else
                {
                    _ghost.HidePlacement();
                    readout.Reason = "No snap point in reach";
                }

                Build = readout;
            }
            else if (held != null && held.IsPlaceable && Target.Kind != TargetKind.None
                     && Target.Kind != TargetKind.Entity && Target.Kind != TargetKind.Fixture)
            {
                Vector3Int cell = Target.PlaceCell;
                bool valid;
                Vector3Int footprint;

                if (held.placeableStructure != null)
                {
                    footprint = held.placeableStructure.footprint;
                    valid = _structures.CanPlace(held.placeableStructure, cell, _placeRotation) && !IntersectsPlayer(cell, footprint, _placeRotation);
                }
                else if (held.placeableVehicle != null)
                {
                    var chassis = held.placeableVehicle.chassisSize;
                    footprint = new Vector3Int(
                        Mathf.Max(1, Mathf.CeilToInt(chassis.x * 2f)),
                        Mathf.Max(1, Mathf.CeilToInt(chassis.y * 2f)),
                        Mathf.Max(1, Mathf.CeilToInt(chassis.z * 2f)));
                    valid = _voxels.IsSolid(cell.x, cell.y - 1, cell.z) && !IntersectsPlayer(cell, footprint, 0);
                }
                else
                {
                    footprint = Vector3Int.one;
                    valid = CanPlaceBlockAt(cell);
                }

                _ghost.ShowPlacement(cell, footprint, _placeRotation, valid);
                Build = default(BuildReadout); // deployables and blocks are not the snap grid
            }
            else
            {
                _ghost.HidePlacement();
                Build = default(BuildReadout);
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

        /// <summary>The grids are handed over after the worlds exist, not at Init.</summary>
        public void BindUtilities(PowerWorld power, FluidWorld fluid)
        {
            _power = power;
            _fluid = fluid;
            if (Wiring == null) Wiring = new WireTool();
        }

        /// <summary>True while the wire tool is in hand, so the HUD can switch layers.</summary>
        public bool IsWiring
        {
            get
            {
                var held = _inventory != null ? _inventory.SelectedItem : null;
                return held != null && held.stringId == MadVoxel.Content.ItemIds.WireTool;
            }
        }

        /// <summary>
        /// Routes a click to whichever graph is under the crosshair. Returns true when
        /// the wire tool consumed it, so mining and melee never fire through a click
        /// meant for a socket.
        /// </summary>
        bool HandleWiring(bool primary)
        {
            if (!IsWiring || Wiring == null) return false;

            var collider = Target.Structure != null ? Target.Structure.GetComponent<Collider>() : null;
            var powerDevice = Target.Structure != null
                ? Target.Structure.GetComponentInChildren<PowerDeviceStructure>() : null;
            var fluidDevice = Target.Structure != null
                ? Target.Structure.GetComponentInChildren<FluidDeviceStructure>() : null;

            if (!primary)
            {
                Wiring.Cut(_power != null ? _power.Graph : null, powerDevice,
                           _fluid != null ? _fluid.Graph : null, fluidDevice);
                return true;
            }

            // A pump is on both graphs. While a hose is in the air it is a fitting;
            // otherwise the grid wins, because that is what the tool is mostly for.
            if (fluidDevice != null && (Wiring.PendingFluidNode != 0 || powerDevice == null))
            {
                Wiring.ClickFluid(_fluid != null ? _fluid.Graph : null, fluidDevice);
                return true;
            }

            if (powerDevice != null)
            {
                Wiring.ClickPower(_power != null ? _power.Graph : null, powerDevice);
                return true;
            }

            if (Wiring.HasPending)
            {
                Notifications.Post("Point at a device to finish the line");
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------- ranged

        /// <summary>How far through the draw, 0 to 1. The HUD reads this.</summary>
        public float Draw01 { get; private set; }

        /// <summary>True while a bow is actually drawn, so the HUD can show the arc.</summary>
        public bool IsDrawing { get { return Draw01 > 0f; } }

        float _drawHeld;

        /// <summary>
        /// Returns true when a bow consumed the input this frame, so nothing else acts
        /// on it. Switching away mid-draw, running out of arrows or dying all let the
        /// draw go without loosing - you keep the arrow.
        /// </summary>
        bool HandleBow()
        {
            var held = _inventory.SelectedItem;
            bool isBow = held != null && held.IsRanged;

            if (!isBow)
            {
                _drawHeld = 0f;
                Draw01 = 0f;
                return false;
            }

            if (BestArrow(held) == null)
            {
                if (InputBridge.PrimaryDown)
                {
                    Notifications.PostFormat("Out of {0}", held.ammoItem.displayName);
                    Audio.GameAudio.Play(Audio.Sound.Denied);
                }

                _drawHeld = 0f;
                Draw01 = 0f;
                return true;
            }

            if (InputBridge.PrimaryHeld)
            {
                // The creak, once, as the string goes back - not every frame it is held.
                if (_drawHeld <= 0f) Audio.GameAudio.Play(Audio.Sound.BowDraw, 0.06f);

                _drawHeld += Time.deltaTime;
                Draw01 = Combat.Ballistics.Draw01(_drawHeld, held.drawSeconds);
                ResetMining();
                return true;
            }

            // Let go.
            if (Draw01 > 0f)
            {
                float draw = Draw01;
                _drawHeld = 0f;
                Draw01 = 0f;

                Loose(held, draw);
            }

            return true;
        }

        /// <summary>
        /// The best arrow in the bag, or null when there is none.
        ///
        /// Chosen at the string rather than declared on the bow, because a bow that can
        /// only ever fire the arrow its definition names makes every better arrow in
        /// the game uncraftable in practice - which is exactly what the iron arrow was.
        /// Best means hardest-hitting: if you are carrying them, you meant to use them.
        /// </summary>
        ItemDefinition BestArrow(ItemDefinition bow)
        {
            if (bow == null || bow.ammoItem == null) return null;

            ItemDefinition best = null;
            var bag = _inventory.Bag;

            for (int i = 0; i < bag.Size; i++)
            {
                var stack = bag[i];
                if (stack.IsEmpty || stack.Item == null) continue;

                // Ammunition is anything that carries a head and is not itself a bow.
                if (stack.Item.IsRanged || stack.Item.category != ItemCategory.Ammo) continue;
                if (best == null || stack.Item.rangedDamage > best.rangedDamage) best = stack.Item;
            }

            return best;
        }

        void Loose(ItemDefinition bow, float draw01)
        {
            if (!Combat.Ballistics.CanRelease(draw01))
            {
                // Too little draw to be worth an arrow, so the arrow is kept.
                Notifications.Post("Not drawn enough");
                Audio.GameAudio.Play(Audio.Sound.Denied);
                return;
            }

            // Stamina scales with the draw, so a panicked snap shot is cheap and a
            // held aim is what tires you out.
            if (!_stats.TrySpendStamina(bow.drawStamina * draw01))
            {
                Notifications.Post("Too tired to draw");
                Audio.GameAudio.Play(Audio.Sound.Denied);
                return;
            }

            var arrow = BestArrow(bow);
            if (arrow == null) return;

            _inventory.Bag.Remove(arrow, 1);

            // Pitched by the draw, so the bow tells you how hard you pulled it.
            Audio.GameAudio.Play(Audio.Sound.BowRelease, 0.05f, 0.6f + draw01 * 0.4f);

            float speed = Combat.Ballistics.LaunchSpeed(bow.minLaunchSpeed, bow.maxLaunchSpeed, draw01);
            // Bow plus head: a better arrow is a real upgrade without needing a second
            // bow, and the draw scales the whole thing rather than only half of it.
            float rating = bow.rangedDamage + Mathf.Max(0f, arrow.rangedDamage);
            float damage = Combat.Ballistics.Damage(rating, draw01)
                           * Perks.Multiplier(PerkEffectType.RangedDamageMultiplier);

            // From the eye, along the crosshair: the arc starts where the player is
            // looking, so what they aimed at is what the arrow was pointed at.
            var origin = _camera.transform.position + _camera.transform.forward * 0.45f;

            // Parented to whatever holds the player - the world root - so arrows in
            // flight die with the world rather than hanging in an empty scene after a
            // quit to menu.
            Combat.Arrow.Fire(transform.parent, origin, _camera.transform.forward, speed, damage,
                              bow.toolTier, arrow, gameObject);

            if (bow.HasDurability) _inventory.WearSelected(1);
        }

        void HandlePrimary()
        {
            if (HandleWiring(true)) { ResetMining(); return; }

            // A swing beats the crosshair.
            //
            // The probe is a single ray, and a shambler's physics capsule is narrower
            // than the body drawn over it, so the ray happily finds the wall behind a
            // zombie that is chewing on you - and you mine it while you are eaten. If
            // something hostile is closer than whatever the crosshair landed on, the
            // swing goes to the zombie. That is what the player meant.
            var swing = FindSwingTarget();
            if (swing != null && SwingBeatsCrosshair(swing))
            {
                Attack(swing);
                ResetMining();
                return;
            }

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
                    SwingAtNothing();
                    ResetMining();
                    break;
            }
        }

        /// <summary>
        /// A swing that connects with nothing still has to sound like a swing.
        ///
        /// Silence on a miss is the same silence a broken button makes, and a player
        /// who cannot tell those apart stops trusting the button. Costs no stamina -
        /// you are paying for the hit, not for the gesture.
        /// </summary>
        void SwingAtNothing()
        {
            if (Time.time < _nextAttackTime) return;

            var held = _inventory.SelectedItem;
            _nextAttackTime = Time.time + (held != null ? Mathf.Max(0.2f, held.attackCooldown) : BareHandCooldown);
            Audio.GameAudio.Play(Audio.Sound.MeleeSwing, 0.12f, 0.7f);
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
            // Asked before the block goes. Clearing it raises BlockChanged, and this
            // class listens to that in order to forget cells whoever emptied them - so
            // by the time SetBlock returns, the answer would always be "no" and every
            // block the player laid would pay full XP on the way back out.
            bool wasPlayerPlaced = _playerPlaced.Contains(cell);

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

            Audio.GameAudio.PlayAt(Audio.Sound.BlockBreak, cell + Vector3.one * 0.5f);

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

        /// <summary>
        /// What is inside the swing, or null.
        ///
        /// Deliberately reads the live zombie list rather than sweeping physics. An
        /// overlap query would come back full of terrain, walls and floors and would
        /// have to be sieved for the one collider that matters, and in a base it would
        /// overflow its buffer before it reached the zombie standing in the doorway.
        /// The list is short, exact, and already maintained.
        /// </summary>
        IMeleeTarget FindSwingTarget()
        {
            if (Spawns == null || _camera == null) return null;

            var alive = Spawns.Alive;
            if (alive == null || alive.Count == 0) return null;

            if (_swingBuffer.Length < alive.Count)
            {
                _swingBuffer = new Combat.MeleeArc.Candidate[Mathf.NextPowerOfTwo(alive.Count)];
            }

            Vector3 eye = _camera.transform.position;
            Vector3 forward = _camera.transform.forward;

            int count = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                var zombie = alive[i];
                if (zombie == null || !zombie.IsAlive) continue;

                _swingBuffer[count] = new Combat.MeleeArc.Candidate
                {
                    Id = i,
                    Centre = zombie.CentreOfMass,
                    Radius = zombie.BodyRadius
                };
                count++;
            }

            // Resolve, check the line, and if a wall is in the way drop that body and
            // ask again. A single pass would give up the whole swing because the arc's
            // favourite target happened to be behind a door - while a shambler stood
            // in the open a metre to the left.
            for (int attempt = 0; attempt < 4 && count > 0; attempt++)
            {
                var result = Combat.MeleeArc.Resolve(eye, forward, _swingBuffer, count);
                if (!result.Hit) return null;

                var chosen = alive[result.Id];
                if (chosen != null && chosen.IsAlive)
                {
                    IMeleeTarget inTheWay;
                    if (!Blocked(eye, chosen, out inTheWay))
                    {
                        _swingDistance = result.Distance;
                        return chosen;
                    }

                    // Another body between you and it. That one is nearer and the
                    // player is swinging through it either way, so it takes the blow.
                    if (inTheWay != null)
                    {
                        _swingDistance = result.Distance;
                        return inTheWay;
                    }
                }

                count = Drop(result.Id, count);
            }

            return null;
        }

        /// <summary>Removes a candidate by its id, keeping the buffer dense.</summary>
        int Drop(int id, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_swingBuffer[i].Id != id) continue;
                _swingBuffer[i] = _swingBuffer[count - 1];
                return count - 1;
            }
            return count;
        }

        /// <summary>
        /// Is something solid between the eye and the body?
        ///
        /// <paramref name="inTheWay"/> comes back set when the obstruction is itself
        /// something you could hit, which the caller would rather swing at than
        /// abandon the whole attack over.
        /// </summary>
        bool Blocked(Vector3 eye, IMeleeTarget target, out IMeleeTarget inTheWay)
        {
            inTheWay = null;

            Vector3 toTarget = target.CentreOfMass - eye;
            float distance = toTarget.magnitude;
            if (distance <= 0.01f) return false;

            RaycastHit hit;
            if (!Physics.Raycast(eye, toTarget / distance, out hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Hitting the target itself, or our own capsule on the way out, is clear.
            if (hit.collider.transform.IsChildOf(transform)) return false;

            var blocker = hit.collider.GetComponentInParent<IMeleeTarget>();
            if (ReferenceEquals(blocker, target)) return false;

            if (blocker != null && blocker.IsAlive) inTheWay = blocker;
            return true;
        }

        /// <summary>
        /// Should the swing take the shot instead of the crosshair?
        ///
        /// Only when the body is actually nearer than whatever the ray found. Looking
        /// past a zombie at a wall two metres behind it is a swing at the zombie;
        /// mining a wall with one shambling somewhere off to the side is still mining.
        /// </summary>
        bool SwingBeatsCrosshair(IMeleeTarget swing)
        {
            if (Target.Kind == TargetKind.None || Target.Kind == TargetKind.Fixture) return true;

            // A hammer is for repairing, not for fighting. Someone patching a wall
            // mid-siege must not find themselves punching instead.
            var held = _inventory.SelectedItem;
            if (held != null && held.toolType == ToolType.Hammer
                && (Target.Kind == TargetKind.BuildPiece || Target.Kind == TargetKind.Structure))
            {
                return false;
            }

            return _swingDistance <= Target.Distance;
        }

        void Attack(IDamageable damageable)
        {
            if (damageable == null || Time.time < _nextAttackTime) return;

            var held = _inventory.SelectedStack;
            float damage = held.Item != null && held.Item.meleeDamage > 0f ? held.Item.meleeDamage : 4f;
            damage *= Perks.Multiplier(PerkEffectType.MeleeDamageMultiplier);
            float cooldown = held.Item != null ? Mathf.Max(0.2f, held.Item.attackCooldown) : BareHandCooldown;

            // Stamina is checked before the cooldown is spent, and it says so when it
            // refuses. The old order burned the cooldown on a swing that never
            // happened and reported nothing, so a player who had just sprinted away
            // from a shambler stood there mashing a button that was silently dead.
            if (!_stats.TrySpendStamina(3f))
            {
                if (Time.time >= _nextWindedMessage)
                {
                    _nextWindedMessage = Time.time + 1.5f;
                    Notifications.Post("Too winded to swing");
                    Audio.GameAudio.Play(Audio.Sound.Denied);
                }
                return;
            }

            _nextAttackTime = Time.time + cooldown;
            Audio.GameAudio.Play(Audio.Sound.MeleeSwing);

            damageable.ApplyDamage(new DamageInfo
            {
                Amount = damage,
                Kind = DamageKind.Melee,
                Point = transform.position,
                Direction = _camera.transform.forward,
                Source = gameObject,
                ToolTier = held.Item != null ? held.Item.toolTier : 0
            });

            // Flesh and stone have to be told apart by ear alone: in a fight the
            // player is looking at the zombie, not at the wall they just clipped.
            Audio.GameAudio.PlayAt(
                damageable is IMeleeTarget ? Audio.Sound.MeleeHitFlesh : Audio.Sound.MeleeHitHard,
                ImpactPoint(damageable));

            LandedHit(damageable, damage);

            if (held.Item != null && held.Item.HasDurability && _inventory.WearSelected(1))
            {
                Notifications.PostFormat("{0} broke", held.Item.displayName);
            }
        }

        /// <summary>Where a blow landed, for putting its sound in the world.</summary>
        Vector3 ImpactPoint(IDamageable victim)
        {
            var body = victim as IMeleeTarget;
            if (body != null) return body.CentreOfMass;
            return Target.Kind != TargetKind.None ? Target.HitPoint : transform.position;
        }

        /// <summary>
        /// Tells the HUD a swing connected.
        ///
        /// Melee carries no hit zone. A swing is a cone - it finds the player a target
        /// rather than a point - so there is no honest way to say where on a body it
        /// landed, and handing out a headshot bonus at random is worse than not having
        /// one. Precision is the bow's advantage, and this is where that is decided.
        /// </summary>
        void LandedHit(IDamageable victim, float damage)
        {
            Combat.CombatEvents.ReportHit(new Combat.HitReport
            {
                Victim = victim,
                Damage = damage,
                Zone = Combat.HitZone.None,
                Killed = !victim.IsAlive,
                Point = ImpactPoint(victim)
            });
        }

        // ------------------------------------------------------------------ placing

        void HandleSecondary()
        {
            if (HandleWiring(false)) return;

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

            // Muck goes on by hand, one cell at a time. Slow, and meant to be - it is
            // the reason a spreader is worth building.
            if (held.Item.stringId == MadVoxel.Content.ItemIds.Compost && Target.Kind == TargetKind.Block)
            {
                FertiliseTarget();
                return;
            }

            if (Target.Kind == TargetKind.None) return;

            if (held.Item.placeableBuildPiece != null) PlaceBuildPiece(held.Item);
            else if (held.Item.placeableStructure != null) PlaceStructure(held.Item);
            else if (held.Item.placeableVehicle != null) PlaceVehicle(held.Item);
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

        void FertiliseTarget()
        {
            if (_fields == null) return;

            // Asked before anything is spread, not after. Three refusals, because they
            // need three different answers - and note that "tillable" covers ground
            // both before and after the plow, so it cannot tell worked soil from scrub
            // on its own.
            var cell = Target.BlockCell;
            if (!_fields.IsTillable(cell) || !_fields.IsWorked(cell) || !_fields.TryFertilise(cell))
            {
                string why;
                if (!_fields.IsTillable(cell)) why = "Nothing here to feed";
                else if (!_fields.IsWorked(cell)) why = "Break the ground before feeding it";
                else why = "This ground has all the muck it can take";

                Notifications.Post(why);
                Audio.GameAudio.Play(Audio.Sound.Denied);
                return;
            }

            _inventory.ConsumeSelected(1);
            _progression.AddXp(1.5f, XpSource.Harvest);
            Audio.GameAudio.PlayAt(Audio.Sound.Place, Target.HitPoint, 0.15f, 0.5f);
            Notifications.Post("Muck spread");
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
            Audio.GameAudio.PlayAt(Audio.Sound.Place, Target.HitPoint);
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
            Audio.GameAudio.PlayAt(Audio.Sound.Place, Target.HitPoint);

            if (def.kind == StructureKind.ToolCupboard)
            {
                Notifications.PostFormat("Land claimed - {0}m protected", Mathf.RoundToInt(placed.Definition.claimRadius > 0f ? placed.Definition.claimRadius : _config.claimRadius));
            }
        }

        /// <summary>
        /// Sets a machine down where you are looking. Unlike a deployable it does not
        /// occupy cells - it drives away - so the only check is that there is ground
        /// under it and the player is not standing in it.
        /// </summary>
        void PlaceVehicle(ItemDefinition item)
        {
            if (Vehicles == null) return;

            var def = item.placeableVehicle;
            var cell = Target.PlaceCell;
            var footprint = new Vector3Int(
                Mathf.Max(1, Mathf.CeilToInt(def.chassisSize.x * 2f)),
                Mathf.Max(1, Mathf.CeilToInt(def.chassisSize.y * 2f)),
                Mathf.Max(1, Mathf.CeilToInt(def.chassisSize.z * 2f)));

            if (IntersectsPlayer(cell, footprint, 0))
            {
                Notifications.Post("Stand back to set it down");
                return;
            }

            if (!_voxels.IsSolid(cell.x, cell.y - 1, cell.z))
            {
                Notifications.Post("Nothing to set it down on");
                return;
            }

            // Facing away from the player, so you step off and it is pointing at the field.
            float yaw = transform.eulerAngles.y;
            var spawn = new Vector3(cell.x + 0.5f, cell.y + 0.1f, cell.z + 0.5f);

            if (Vehicles.Spawn(def, spawn, yaw) == null) return;

            _inventory.ConsumeSelected(1);
            Notifications.PostFormat("{0} set down - E to get on", def.displayName);
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
