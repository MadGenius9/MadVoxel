using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Fluid
{
    public enum PipeResult
    {
        Ok,
        UnknownFitting,
        SameFitting,
        TooFar,
        NoOutlet,
        NoInlet,
        OutputsFull,
        AlreadyPiped,
        WouldLoop
    }

    /// <summary>What the world is doing to the water this tick.</summary>
    public struct FluidConditions
    {
        /// <summary>Biome times weather. Drought on the dry flats is brutal.</summary>
        public float PumpMultiplier;
        /// <summary>A frost night stops every freezable tap that has nothing warm nearby.</summary>
        public bool FreezingOutside;

        public static FluidConditions Normal
        {
            get { return new FluidConditions { PumpMultiplier = 1f, FreezingOutside = false }; }
        }
    }

    /// <summary>
    /// The one fluid graph: pump to pipe to tank to tap. It borrows Rust's shape and
    /// none of Rust's industrial complexity - there is no mixing, no pressure and no
    /// fluid logic.
    ///
    /// The model is deliberately a pool rather than a per-pipe simulation. Everything a
    /// pump can still reach shares its tanks, inflow fills that pool, and outlets drain
    /// it. That gives the four behaviours the game actually needs - a tank buffers the
    /// night, a dry well starves the tap, a broken pipe kills everything past it, and a
    /// sprinkler competes with a sink for the same litres - without pretending to model
    /// head pressure nobody can see.
    ///
    /// A broken fitting is a wall and a leak at once: the walk stops there, and the
    /// litres that would have passed are lost rather than banked.
    /// </summary>
    public class FluidGraph
    {
        readonly Dictionary<int, FluidNode> _nodes = new Dictionary<int, FluidNode>();
        readonly List<FluidNode> _ordered = new List<FluidNode>();

        int _nextId = 1;

        /// <summary>Metres added to every hose by the Electrician perk.</summary>
        public float ExtraReach;

        readonly Queue<FluidNode> _frontier = new Queue<FluidNode>();
        readonly List<FluidNode> _live = new List<FluidNode>();

        public IReadOnlyList<FluidNode> Nodes { get { return _ordered; } }

        /// <summary>Litres a minute the pumps actually delivered on the last tick.</summary>
        public float InflowLitresPerMinute { get; private set; }

        /// <summary>Litres a minute lost through broken fittings.</summary>
        public float LeakLitresPerMinute { get; private set; }

        /// <summary>Litres a minute the outlets pulled.</summary>
        public float DrawLitresPerMinute { get; private set; }

        /// <summary>Litres standing in every reachable tank.</summary>
        public float StoredLitres { get; private set; }
        public float CapacityLitres { get; private set; }

        // ------------------------------------------------------------------- graph

        public FluidNode Add(FluidDeviceDefinition definition, Vector3 position, int forcedId = 0)
        {
            if (definition == null) return null;

            int id = forcedId > 0 ? forcedId : _nextId;
            if (id >= _nextId) _nextId = id + 1;

            var node = new FluidNode { Id = id, Definition = definition, Position = position };
            _nodes[id] = node;
            _ordered.Add(node);
            return node;
        }

        public FluidNode Get(int id)
        {
            FluidNode node;
            return _nodes.TryGetValue(id, out node) ? node : null;
        }

        public void Remove(int id)
        {
            FluidNode node;
            if (!_nodes.TryGetValue(id, out node)) return;

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

            _nodes.Remove(id);
            _ordered.Remove(node);
        }

        public void Clear()
        {
            _nodes.Clear();
            _ordered.Clear();
            _nextId = 1;
            InflowLitresPerMinute = LeakLitresPerMinute = DrawLitresPerMinute = 0f;
            StoredLitres = CapacityLitres = 0f;
        }

        // ---------------------------------------------------------------- plumbing

        public PipeResult CanConnect(int fromId, int toId)
        {
            if (fromId == toId) return PipeResult.SameFitting;

            var from = Get(fromId);
            var to = Get(toId);
            if (from == null || to == null) return PipeResult.UnknownFitting;

            if (from.Definition.IsOutlet) return PipeResult.NoOutlet;
            if (to.Kind == FluidDeviceKind.Pump) return PipeResult.NoInlet;

            if (from.Outputs.Contains(toId)) return PipeResult.AlreadyPiped;
            if (from.Outputs.Count >= Mathf.Max(1, from.Definition.maxOutputs)) return PipeResult.OutputsFull;

            float reach = from.Definition.maxPipeLength + Mathf.Max(0f, ExtraReach);
            if ((to.Position - from.Position).sqrMagnitude > reach * reach) return PipeResult.TooFar;

            if (Reaches(to, fromId)) return PipeResult.WouldLoop;
            return PipeResult.Ok;
        }

        public PipeResult Connect(int fromId, int toId)
        {
            var verdict = CanConnect(fromId, toId);
            if (verdict != PipeResult.Ok) return verdict;

            Get(fromId).Outputs.Add(toId);
            Get(toId).Inputs.Add(fromId);
            return PipeResult.Ok;
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

        bool Reaches(FluidNode start, int targetId)
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
                    if (!seen.Add(node.Outputs[i])) continue;
                    var child = Get(node.Outputs[i]);
                    if (child != null) _frontier.Enqueue(child);
                }
            }
            return false;
        }

        public static string Describe(PipeResult result)
        {
            switch (result)
            {
                case PipeResult.Ok: return "";
                case PipeResult.SameFitting: return "That is the same fitting";
                case PipeResult.UnknownFitting: return "No fitting there";
                case PipeResult.TooFar: return "Too far - add a hose section";
                case PipeResult.NoOutlet: return "A tap has no outlet";
                case PipeResult.NoInlet: return "A pump has no inlet";
                case PipeResult.OutputsFull: return "That outlet is taken";
                case PipeResult.AlreadyPiped: return "Already piped";
                case PipeResult.WouldLoop: return "That would loop the line";
                default: return "Cannot pipe that";
            }
        }

        // ------------------------------------------------------------------- solve

        public void Tick(float gameHours, FluidConditions conditions)
        {
            gameHours = Mathf.Max(0f, gameHours);

            Reset();
            MarkLive(conditions);
            MoveWater(gameHours);
        }

        void Reset()
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                _ordered[i].IsLive = false;
                _ordered[i].FlowLitresPerMinute = 0f;
            }
            InflowLitresPerMinute = LeakLitresPerMinute = DrawLitresPerMinute = 0f;
            StoredLitres = CapacityLitres = 0f;
        }

        /// <summary>
        /// Walks out from every working pump. A broken fitting is where the walk stops:
        /// that pump's share leaks away and nothing past it is live, which is exactly
        /// the "break a pipe and the tap runs dry" the storm needs.
        /// </summary>
        void MarkLive(FluidConditions conditions)
        {
            _live.Clear();
            _frontier.Clear();

            var seen = new HashSet<int>();
            float multiplier = Mathf.Max(0f, conditions.PumpMultiplier);

            for (int i = 0; i < _ordered.Count; i++)
            {
                var pump = _ordered[i];
                if (pump.Kind != FluidDeviceKind.Pump) continue;

                if (!pump.CanPump)
                {
                    // A pump that cannot run is not a leak; it simply does nothing.
                    continue;
                }

                float delivered = pump.Definition.litresPerMinute * multiplier;
                pump.FlowLitresPerMinute = delivered;
                pump.IsLive = true;

                if (pump.IsBroken)
                {
                    LeakLitresPerMinute += delivered;
                    continue;
                }

                InflowLitresPerMinute += delivered;

                if (seen.Add(pump.Id)) _live.Add(pump);
                _frontier.Enqueue(pump);
            }

            while (_frontier.Count > 0)
            {
                var node = _frontier.Dequeue();

                for (int i = 0; i < node.Outputs.Count; i++)
                {
                    var child = Get(node.Outputs[i]);
                    if (child == null) continue;

                    // Broken or shut fittings are walls. Everything past them is dry.
                    if (child.IsBroken || !child.SwitchedOn) continue;

                    // A frozen tap is live in the plumbing sense but delivers nothing,
                    // so it is excluded here rather than special-cased at every outlet.
                    if (child.Kind == FluidDeviceKind.Tap && child.Definition.freezable
                        && conditions.FreezingOutside && !child.IsFrozen)
                    {
                        child.IsFrozen = true;
                    }
                    else if (!conditions.FreezingOutside && child.IsFrozen)
                    {
                        child.IsFrozen = false;
                    }

                    if (!seen.Add(child.Id)) continue;

                    child.IsLive = true;
                    _live.Add(child);
                    _frontier.Enqueue(child);
                }
            }
        }

        void MoveWater(float gameHours)
        {
            // Everything the pumps can still reach shares one pool.
            for (int i = 0; i < _live.Count; i++)
            {
                var node = _live[i];
                if (node.Kind != FluidDeviceKind.Tank) continue;

                StoredLitres += node.Litres;
                CapacityLitres += node.Capacity;
            }

            if (gameHours <= 0f) return;

            float minutes = gameHours * 60f;

            // Fill first, so a pump that started this tick can feed a tap this tick.
            float incoming = InflowLitresPerMinute * minutes;
            if (incoming > 0f) Fill(incoming);

            // Then the standing outlets - sprinklers and anything else that runs on its
            // own. Taps are pulled by hand and charge through Draw().
            float wanted = 0f;
            for (int i = 0; i < _live.Count; i++)
            {
                var node = _live[i];
                if (node.Kind != FluidDeviceKind.Sprinkler || !node.SwitchedOn) continue;
                wanted += node.Definition.drawLitresPerMinute;
            }

            if (wanted > 0f)
            {
                float requested = wanted * minutes;
                float served = Drain(requested, InflowLitresPerMinute * minutes);
                float ratio = requested > 0f ? served / requested : 0f;

                DrawLitresPerMinute = wanted * ratio;

                for (int i = 0; i < _live.Count; i++)
                {
                    var node = _live[i];
                    if (node.Kind != FluidDeviceKind.Sprinkler) continue;

                    node.FlowLitresPerMinute = node.Definition.drawLitresPerMinute * ratio;
                    // A sprinkler that only got a trickle is not watering anything.
                    node.IsLive = ratio > 0.5f;
                }
            }

            RecountStorage();
        }

        void Fill(float litres)
        {
            for (int i = 0; i < _live.Count && litres > 0f; i++)
            {
                var node = _live[i];
                if (node.Kind != FluidDeviceKind.Tank) continue;

                float room = node.Capacity - node.Litres;
                if (room <= 0f) continue;

                float take = Mathf.Min(room, litres);
                node.Litres += take;
                litres -= take;
            }

            // Overflow with nowhere to go is simply spilled. A pump with no tank still
            // feeds a tap directly, which is what the direct-flow allowance below covers.
        }

        /// <summary>
        /// Takes litres from the tanks, and lets a tankless line pass its inflow
        /// straight through so a pump wired directly to a tap still gives water.
        /// </summary>
        float Drain(float litres, float directAllowance)
        {
            float served = 0f;

            float direct = Mathf.Min(litres, Mathf.Max(0f, directAllowance));
            served += direct;
            litres -= direct;

            for (int i = 0; i < _live.Count && litres > 0f; i++)
            {
                var node = _live[i];
                if (node.Kind != FluidDeviceKind.Tank || node.Litres <= 0f) continue;

                float take = Mathf.Min(node.Litres, litres);
                node.Litres -= take;
                litres -= take;
                served += take;
            }

            return served;
        }

        void RecountStorage()
        {
            StoredLitres = 0f;
            CapacityLitres = 0f;

            for (int i = 0; i < _live.Count; i++)
            {
                var node = _live[i];
                if (node.Kind != FluidDeviceKind.Tank) continue;

                StoredLitres += node.Litres;
                CapacityLitres += node.Capacity;
            }
        }

        /// <summary>
        /// A hand pull on a tap: a drink, or a bottle. Returns the litres actually
        /// delivered, which is zero on a dry, dead or frozen line.
        /// </summary>
        public float DrawFromOutlet(int outletId, float litres)
        {
            var outlet = Get(outletId);
            if (outlet == null || !outlet.IsLive || outlet.IsBroken || outlet.IsFrozen) return 0f;
            if (outlet.Kind != FluidDeviceKind.Tap) return 0f;

            // A tap can pull from the tanks, or straight off a running pump.
            float served = Drain(litres, InflowLitresPerMinute / 60f);
            RecountStorage();
            return served;
        }

        /// <summary>Litres standing in one tank, for the Claim Slate readout.</summary>
        public float LitresIn(int tankId)
        {
            var node = Get(tankId);
            return node != null ? node.Litres : 0f;
        }
    }
}
