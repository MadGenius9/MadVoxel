using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Power
{
    /// <summary>Why a wire was refused. The wire tool shows these verbatim.</summary>
    public enum WireResult
    {
        Ok,
        UnknownDevice,
        SameDevice,
        TooFar,
        NoOutputSocket,
        NoInputSocket,
        OutputsFull,
        AlreadyWired,
        WouldLoop
    }

    /// <summary>What the world is doing to the grid this tick.</summary>
    public struct PowerConditions
    {
        public bool IsDay;
        /// <summary>Weather times biome. A storm is zero.</summary>
        public float SolarMultiplier;

        public static PowerConditions Daylight
        {
            get { return new PowerConditions { IsDay = true, SolarMultiplier = 1f }; }
        }
    }

    /// <summary>
    /// The one power graph. Sources feed wires, wires pass through relays and switches,
    /// and consumers at the far end either get their watts or go dark.
    ///
    /// Three decisions shape the whole thing:
    ///
    /// * **Reach comes from relays, not from wire.** Every device has a short maximum
    ///   wire length. A relay's reach is longer, and that is the only way to cover
    ///   ground. You cannot run a kilometre of free cable to a distant pump.
    /// * **A brown-out feeds the near devices first.** When demand beats supply, the
    ///   grid walks outward from the source and pays for devices until the budget runs
    ///   out. The light beside the generator stays on; the pump at the end of the line
    ///   is what dies. That is legible in a way that "everything dims" is not.
    /// * **Batteries are the night shift.** Solar and generators feed the grid and put
    ///   their surplus in the bank; the bank only pushes when the grid is short. Fuel
    ///   empty plus dark plus flat battery is a dead grid, and every consumer knows it.
    ///
    /// The whole class is engine-free so it can be solved in a test, and so a save can
    /// rebuild the graph before a single deployable has been spawned.
    /// </summary>
    public class PowerGraph
    {
        readonly Dictionary<int, PowerNode> _nodes = new Dictionary<int, PowerNode>();
        readonly List<PowerNode> _ordered = new List<PowerNode>();

        int _nextId = 1;

        /// <summary>
        /// Metres added to every wire by the Electrician perk. It lives on the graph
        /// rather than on each device so a rank bought mid-game applies at once,
        /// without rewriting a single definition.
        /// </summary>
        public float ExtraReach;

        // Scratch, reused every tick so a grid does not allocate once a second.
        readonly Queue<PowerNode> _frontier = new Queue<PowerNode>();
        readonly List<PowerNode> _reached = new List<PowerNode>();

        public IReadOnlyList<PowerNode> Nodes { get { return _ordered; } }

        /// <summary>Total watts sources put out on the last tick.</summary>
        public float SupplyWatts { get; private set; }

        /// <summary>Total watts the live consumers asked for on the last tick.</summary>
        public float DemandWatts { get; private set; }

        /// <summary>Watts the battery banks had to make up.</summary>
        public float BatteryWatts { get; private set; }

        public float StoredWattHours { get; private set; }
        public float StorageCapacityWattHours { get; private set; }

        /// <summary>True when demand beat everything the grid could find.</summary>
        public bool IsBrownOut { get; private set; }

        // ------------------------------------------------------------------- graph

        public PowerNode Add(PowerDeviceDefinition definition, Vector3 position, int forcedId = 0)
        {
            if (definition == null) return null;

            int id = forcedId > 0 ? forcedId : _nextId;
            if (id >= _nextId) _nextId = id + 1;

            var node = new PowerNode
            {
                Id = id,
                Definition = definition,
                Position = position,
                FuelLitres = 0f,
                StoredWattHours = 0f
            };

            _nodes[id] = node;
            _ordered.Add(node);
            return node;
        }

        public PowerNode Get(int id)
        {
            PowerNode node;
            return _nodes.TryGetValue(id, out node) ? node : null;
        }

        public void Remove(int id)
        {
            PowerNode node;
            if (!_nodes.TryGetValue(id, out node)) return;

            // A removed device takes its wires with it, both ways, or the graph keeps
            // walking into a hole.
            for (int i = 0; i < node.Outputs.Count; i++)
            {
                var other = Get(node.Outputs[i]);
                if (other != null) other.Inputs.Remove(id);
            }
            for (int i = 0; i < node.Inputs.Count; i++)
            {
                var other = Get(node.Inputs[i]);
                if (other != null) other.Outputs.Remove(id);
            }

            node.IsAlive = false;
            _nodes.Remove(id);
            _ordered.Remove(node);
        }

        public void Clear()
        {
            _nodes.Clear();
            _ordered.Clear();
            _nextId = 1;
            SupplyWatts = DemandWatts = BatteryWatts = 0f;
            StoredWattHours = StorageCapacityWattHours = 0f;
            IsBrownOut = false;
        }

        // ------------------------------------------------------------------ wiring

        /// <summary>The longest wire that may leave a device, for the ghost line.</summary>
        public float ReachOf(int id)
        {
            var node = Get(id);
            if (node == null || node.Definition == null) return 0f;
            return node.Definition.WireReach + Mathf.Max(0f, ExtraReach);
        }

        public WireResult CanConnect(int fromId, int toId)
        {
            if (fromId == toId) return WireResult.SameDevice;

            var from = Get(fromId);
            var to = Get(toId);
            if (from == null || to == null) return WireResult.UnknownDevice;

            // Sinks have no output socket; sources have no input socket.
            if (from.Kind == PowerDeviceKind.Consumer) return WireResult.NoOutputSocket;
            if (to.Kind == PowerDeviceKind.Generator || to.Kind == PowerDeviceKind.SolarBank)
                return WireResult.NoInputSocket;

            if (from.Outputs.Contains(toId)) return WireResult.AlreadyWired;
            if (from.Outputs.Count >= Mathf.Max(1, from.Definition.maxOutputs)) return WireResult.OutputsFull;

            float reach = from.Definition.WireReach + Mathf.Max(0f, ExtraReach);
            if ((to.Position - from.Position).sqrMagnitude > reach * reach) return WireResult.TooFar;

            // A ring would make the brown-out walk ambiguous and buys nothing without
            // logic gates, which this pass deliberately does not have.
            if (Reaches(to, fromId)) return WireResult.WouldLoop;

            return WireResult.Ok;
        }

        public WireResult Connect(int fromId, int toId)
        {
            var verdict = CanConnect(fromId, toId);
            if (verdict != WireResult.Ok) return verdict;

            Get(fromId).Outputs.Add(toId);
            Get(toId).Inputs.Add(fromId);
            return WireResult.Ok;
        }

        public bool Disconnect(int fromId, int toId)
        {
            var from = Get(fromId);
            var to = Get(toId);
            if (from == null || to == null) return false;

            bool removed = from.Outputs.Remove(toId);
            to.Inputs.Remove(fromId);
            return removed;
        }

        /// <summary>Walks downstream looking for a device, so a ring can be refused.</summary>
        bool Reaches(PowerNode start, int targetId)
        {
            _frontier.Clear();
            _frontier.Enqueue(start);

            var seen = new HashSet<int> { start.Id };

            while (_frontier.Count > 0)
            {
                var node = _frontier.Dequeue();
                if (node.Id == targetId) return true;

                for (int i = 0; i < node.Outputs.Count; i++)
                {
                    int next = node.Outputs[i];
                    if (!seen.Add(next)) continue;

                    var child = Get(next);
                    if (child != null) _frontier.Enqueue(child);
                }
            }
            return false;
        }

        public static string Describe(WireResult result)
        {
            switch (result)
            {
                case WireResult.Ok: return "";
                case WireResult.SameDevice: return "That is the same device";
                case WireResult.UnknownDevice: return "No device there";
                case WireResult.TooFar: return "Out of wire range - place a relay";
                case WireResult.NoOutputSocket: return "That device has no power out";
                case WireResult.NoInputSocket: return "A source has no power in";
                case WireResult.OutputsFull: return "Output socket is full - use a splitter";
                case WireResult.AlreadyWired: return "Already wired";
                case WireResult.WouldLoop: return "That would loop the circuit";
                default: return "Cannot wire that";
            }
        }

        // ------------------------------------------------------------------- solve

        /// <summary>
        /// Advances the grid by <paramref name="gameHours"/>. Burns fuel, charges and
        /// drains batteries, and decides which consumers are lit.
        /// </summary>
        public void Tick(float gameHours, PowerConditions conditions)
        {
            gameHours = Mathf.Max(0f, gameHours);

            ResetNodes();
            CollectSupply(conditions);
            MarkReachable();
            Distribute(gameHours);
            BurnAndStore(gameHours);
        }

        void ResetNodes()
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                var node = _ordered[i];
                node.IsPowered = false;
                node.WattsDrawn = 0f;
                node.WattsProduced = 0f;
                node.DistanceFromSource = int.MaxValue;
            }
            SupplyWatts = DemandWatts = BatteryWatts = 0f;
            StoredWattHours = StorageCapacityWattHours = 0f;
            IsBrownOut = false;
        }

        void CollectSupply(PowerConditions conditions)
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                var node = _ordered[i];
                var def = node.Definition;
                if (def == null) continue;

                if (def.kind == PowerDeviceKind.BatteryBank)
                {
                    StoredWattHours += node.StoredWattHours;
                    StorageCapacityWattHours += def.storageWattHours;
                    continue;
                }

                if (!node.CanProduce(conditions.IsDay)) continue;

                float output = def.wattsProduced;
                if (def.kind == PowerDeviceKind.SolarBank)
                {
                    output *= Mathf.Max(0f, conditions.SolarMultiplier);
                }

                node.WattsProduced = output;
                SupplyWatts += output;
            }
        }

        /// <summary>
        /// Floods outward from every live source, through relays, closed switches and
        /// splitters. A device nothing reaches is simply dark - the grid never "almost"
        /// powers something at the end of an open switch.
        /// </summary>
        void MarkReachable()
        {
            _reached.Clear();
            _frontier.Clear();

            for (int i = 0; i < _ordered.Count; i++)
            {
                var node = _ordered[i];
                bool isLiveSource = node.WattsProduced > 0f;

                // A charged battery is a source of reach as well as of watts, or the
                // grid would go dark at dusk even with a full bank.
                bool isChargedBattery = node.Kind == PowerDeviceKind.BatteryBank
                                        && node.SwitchedOn && node.StoredWattHours > 0f;

                if (!isLiveSource && !isChargedBattery) continue;

                node.DistanceFromSource = 0;
                _frontier.Enqueue(node);
                _reached.Add(node);
            }

            while (_frontier.Count > 0)
            {
                var node = _frontier.Dequeue();

                // An open switch is a wall. A relay or splitter that is switched off is
                // the same wall, which is what makes a blackout switch one click.
                if (node.DistanceFromSource > 0 && node.Definition.PassesPower && !node.SwitchedOn) continue;

                for (int i = 0; i < node.Outputs.Count; i++)
                {
                    var child = Get(node.Outputs[i]);
                    if (child == null) continue;
                    if (child.DistanceFromSource <= node.DistanceFromSource + 1) continue;

                    child.DistanceFromSource = node.DistanceFromSource + 1;
                    _reached.Add(child);
                    _frontier.Enqueue(child);
                }
            }

            // Nearest first, insertion order as the tie-break, so a brown-out always
            // browns out the same devices for the same grid.
            _reached.Sort(CompareByDistance);
        }

        static int CompareByDistance(PowerNode a, PowerNode b)
        {
            if (a.DistanceFromSource != b.DistanceFromSource)
                return a.DistanceFromSource.CompareTo(b.DistanceFromSource);
            return a.Id.CompareTo(b.Id);
        }

        void Distribute(float gameHours)
        {
            float budget = SupplyWatts;

            // What the reachable, switched-on devices want.
            float wanted = 0f;
            for (int i = 0; i < _reached.Count; i++) wanted += _reached[i].DemandWatts;
            DemandWatts = wanted;

            // Batteries make up a shortfall, limited both by how fast they can push and
            // by how much is actually in them for the length of this tick.
            if (wanted > budget)
            {
                float shortfall = wanted - budget;
                float available = AvailableBatteryWatts(gameHours);
                BatteryWatts = Mathf.Min(shortfall, available);
                budget += BatteryWatts;
            }

            for (int i = 0; i < _reached.Count; i++)
            {
                var node = _reached[i];
                float draw = node.DemandWatts;
                if (draw <= 0f)
                {
                    // Relays with no upkeep, splitters and closed switches are live as
                    // soon as the flood reaches them.
                    node.IsPowered = true;
                    continue;
                }

                if (draw > budget)
                {
                    IsBrownOut = true;
                    continue;
                }

                budget -= draw;
                node.WattsDrawn = draw;
                node.IsPowered = true;
            }
        }

        /// <summary>
        /// How many watts the banks can actually sustain for this tick: the lower of
        /// their discharge rate and what their stored energy covers for that long.
        /// </summary>
        float AvailableBatteryWatts(float gameHours)
        {
            float rate = 0f;
            float energy = 0f;

            for (int i = 0; i < _ordered.Count; i++)
            {
                var node = _ordered[i];
                if (node.Kind != PowerDeviceKind.BatteryBank || !node.SwitchedOn) continue;
                if (node.StoredWattHours <= 0f) continue;

                rate += node.Definition.dischargeWatts;
                energy += node.StoredWattHours;
            }

            if (gameHours <= 0f) return rate;
            return Mathf.Min(rate, energy / gameHours);
        }

        /// <summary>
        /// Burns the fuel the generators actually earned, then settles the banks: they
        /// take the surplus when there is one and pay out when there is not.
        /// </summary>
        void BurnAndStore(float gameHours)
        {
            if (gameHours <= 0f) return;

            float load = Mathf.Clamp01(SupplyWatts > 0f ? Mathf.Min(DemandWatts, SupplyWatts) / SupplyWatts : 0f);

            for (int i = 0; i < _ordered.Count; i++)
            {
                var node = _ordered[i];
                if (node.Kind != PowerDeviceKind.Generator || node.WattsProduced <= 0f) continue;

                // A generator running one lamp should not drink like it is running the
                // farm, but an idling engine is not free either.
                float rate = node.Definition.fuelLitresPerHour * Mathf.Lerp(0.25f, 1f, load);
                node.FuelLitres = Mathf.Max(0f, node.FuelLitres - rate * gameHours);
            }

            float surplus = SupplyWatts - DemandWatts;

            if (BatteryWatts > 0f)
            {
                DrainBatteries(BatteryWatts * gameHours);
            }
            else if (surplus > 0f)
            {
                ChargeBatteries(surplus * gameHours);
            }

            StoredWattHours = 0f;
            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_ordered[i].Kind == PowerDeviceKind.BatteryBank) StoredWattHours += _ordered[i].StoredWattHours;
            }
        }

        void ChargeBatteries(float wattHours)
        {
            for (int i = 0; i < _ordered.Count && wattHours > 0f; i++)
            {
                var node = _ordered[i];
                if (node.Kind != PowerDeviceKind.BatteryBank || !node.SwitchedOn) continue;

                float room = node.Definition.storageWattHours - node.StoredWattHours;
                if (room <= 0f) continue;

                float take = Mathf.Min(room, wattHours);
                node.StoredWattHours += take;
                wattHours -= take;
            }
        }

        void DrainBatteries(float wattHours)
        {
            for (int i = 0; i < _ordered.Count && wattHours > 0f; i++)
            {
                var node = _ordered[i];
                if (node.Kind != PowerDeviceKind.BatteryBank || !node.SwitchedOn) continue;
                if (node.StoredWattHours <= 0f) continue;

                float take = Mathf.Min(node.StoredWattHours, wattHours);
                node.StoredWattHours -= take;
                wattHours -= take;
            }
        }
    }
}
