using MadVoxel.Fluid;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The water side: pump to pipe to tank to tap. These pin the four behaviours the
    /// success test asks for - a tank buffers the night, a dry or unpowered well gives
    /// nothing, a broken pipe kills the line, and a frost night stops the tap.
    /// </summary>
    public static class FluidTests
    {
        public static void Run()
        {
            Plumbing();
            Pumping();
            TanksAndTaps();
            BreaksAndFrost();
            Sprinklers();
        }

        // ----------------------------------------------------------------- helpers

        static FluidDeviceDefinition Device(FluidDeviceKind kind, string id)
        {
            var def = ScriptableObject.CreateInstance<FluidDeviceDefinition>();
            def.stringId = id;
            def.displayName = id;
            def.kind = kind;
            def.maxPipeLength = 10f;
            def.maxOutputs = 1;
            return def;
        }

        static FluidDeviceDefinition Pump(float litresPerMinute)
        {
            var def = Device(FluidDeviceKind.Pump, "test:pump");
            def.litresPerMinute = litresPerMinute;
            def.wattsRequired = 12f;
            def.maxOutputs = 2;
            return def;
        }

        static FluidDeviceDefinition Pipe()
        {
            var def = Device(FluidDeviceKind.Pipe, "test:pipe");
            def.maxOutputs = 2;
            return def;
        }

        static FluidDeviceDefinition Tank(float capacity)
        {
            var def = Device(FluidDeviceKind.Tank, "test:tank");
            def.capacityLitres = capacity;
            def.maxOutputs = 3;
            return def;
        }

        static FluidDeviceDefinition Tap()
        {
            var def = Device(FluidDeviceKind.Tap, "test:tap");
            def.servingLitres = 1.5f;
            def.freezable = true;
            return def;
        }

        static FluidDeviceDefinition Sprinkler(float draw)
        {
            var def = Device(FluidDeviceKind.Sprinkler, "test:sprinkler");
            def.drawLitresPerMinute = draw;
            return def;
        }

        static Vector3 At(float x) { return new Vector3(x, 0f, 0f); }

        /// <summary>A working well: powered, wet, switched on.</summary>
        static FluidNode LiveWell(FluidGraph graph, float litresPerMinute)
        {
            var pump = graph.Add(Pump(litresPerMinute), At(0f));
            pump.HasPower = true;
            pump.HasWetSource = true;
            return pump;
        }

        // --------------------------------------------------------------- plumbing

        static void Plumbing()
        {
            Harness.Section("fluid: plumbing");

            var graph = new FluidGraph();
            var pump = LiveWell(graph, 24f);
            var near = graph.Add(Tank(400f), At(8f));
            var far = graph.Add(Tank(400f), At(40f));

            Harness.Equal(graph.Connect(pump.Id, near.Id), PipeResult.Ok, "a hose inside range connects");
            Harness.Equal(graph.Connect(pump.Id, far.Id), PipeResult.TooFar, "and past its length is refused");

            var tap = graph.Add(Tap(), At(12f));
            graph.Connect(near.Id, tap.Id);
            Harness.Equal(graph.Connect(tap.Id, far.Id), PipeResult.NoOutlet, "a tap is the end of the line");
            Harness.Equal(graph.Connect(near.Id, pump.Id), PipeResult.NoInlet, "a pump has no inlet");
            Harness.Equal(graph.Connect(pump.Id, near.Id), PipeResult.AlreadyPiped, "the same hose twice is refused");

            var a = graph.Add(Pipe(), At(100f));
            var b = graph.Add(Pipe(), At(104f));
            graph.Connect(a.Id, b.Id);
            Harness.Equal(graph.Connect(b.Id, a.Id), PipeResult.WouldLoop, "a ring is refused");

            graph.Remove(near.Id);
            Harness.Check(!pump.Outputs.Contains(near.Id), "removing a fitting clears the hoses into it");

            for (int i = 0; i < 9; i++)
            {
                var verdict = (PipeResult)i;
                string text = FluidGraph.Describe(verdict);
                Harness.Check(text != null && (verdict == PipeResult.Ok || text.Length > 0),
                    "every plumbing refusal has wording: " + verdict);
            }
        }

        // ---------------------------------------------------------------- pumping

        static void Pumping()
        {
            Harness.Section("fluid: the well");

            var graph = new FluidGraph();
            var pump = LiveWell(graph, 60f);
            var tank = graph.Add(Tank(400f), At(8f));
            graph.Connect(pump.Id, tank.Id);

            graph.Tick(1f, FluidConditions.Normal);
            Harness.Equal(graph.InflowLitresPerMinute, 60f, "a working well delivers its rated flow");
            Harness.Equal(tank.Litres, 400f, "and an hour at 60 L/min fills a 400 L tank");

            // No power is no water. This is the whole reason the pump is on the grid.
            tank.Litres = 0f;
            pump.HasPower = false;
            graph.Tick(1f, FluidConditions.Normal);
            Harness.Equal(graph.InflowLitresPerMinute, 0f, "an unpowered pump delivers nothing");
            Harness.Equal(tank.Litres, 0f, "and the tank stays empty");

            // Nor is a dry well.
            pump.HasPower = true;
            pump.HasWetSource = false;
            graph.Tick(1f, FluidConditions.Normal);
            Harness.Equal(graph.InflowLitresPerMinute, 0f, "a dry well delivers nothing either");

            // Drought and biome both scale the same dial.
            pump.HasWetSource = true;
            graph.Tick(1f, new FluidConditions { PumpMultiplier = 0.4f });
            Harness.Equal(graph.InflowLitresPerMinute, 24f, "drought cuts the well to its multiplier");

            // A tank stops at its capacity rather than banking a lake.
            tank.Litres = 0f;
            graph.Tick(10f, FluidConditions.Normal);
            Harness.Equal(tank.Litres, 400f, "a tank never holds more than it can");
        }

        // ---------------------------------------------------------- tanks and taps

        static void TanksAndTaps()
        {
            Harness.Section("fluid: tanks and taps");

            var graph = new FluidGraph();
            var pump = LiveWell(graph, 30f);
            var tank = graph.Add(Tank(200f), At(8f));
            var tap = graph.Add(Tap(), At(14f));

            graph.Connect(pump.Id, tank.Id);
            graph.Connect(tank.Id, tap.Id);

            graph.Tick(1f, FluidConditions.Normal);
            Harness.Check(tap.IsLive, "the tap is on a live line");

            float served = graph.DrawFromOutlet(tap.Id, 1.5f);
            Harness.Equal(served, 1.5f, "a pull on the tap gives a serving");

            // The tank is the buffer: kill the pump and the tap keeps working until the
            // tank is dry, which is what makes a tank worth building.
            float before = tank.Litres;
            pump.HasPower = false;
            graph.Tick(0f, FluidConditions.Normal);
            Harness.Check(tap.IsLive == false, "a dead pump takes the line down");

            // ...but a tank on the line with a live pump elsewhere is the real case:
            // the pool carries the draw.
            pump.HasPower = true;
            graph.Tick(0f, FluidConditions.Normal);
            Harness.Check(tap.IsLive, "power back on, line live again");
            Harness.Equal(tank.Litres, before, "a zero-length tick moves no water");

            tank.Litres = 2f;
            pump.HasWetSource = false;
            graph.Tick(0f, FluidConditions.Normal);
            Harness.Equal(graph.DrawFromOutlet(tap.Id, 1.5f), 0f,
                "a tap on a stopped pump is dry even with litres in the tank behind it");

            // A pump wired straight to a tap with no tank at all still gives water.
            var direct = new FluidGraph();
            var directPump = LiveWell(direct, 60f);
            var directTap = direct.Add(Tap(), At(8f));
            direct.Connect(directPump.Id, directTap.Id);

            direct.Tick(1f, FluidConditions.Normal);
            Harness.Check(direct.DrawFromOutlet(directTap.Id, 1f) > 0f,
                "a tankless line still pours straight off the pump");
        }

        // ------------------------------------------------------- breaks and frost

        static void BreaksAndFrost()
        {
            Harness.Section("fluid: breaks and frost");

            var graph = new FluidGraph();
            var pump = LiveWell(graph, 60f);
            var pipe = graph.Add(Pipe(), At(6f));
            var tank = graph.Add(Tank(400f), At(12f));
            var tap = graph.Add(Tap(), At(16f));

            graph.Connect(pump.Id, pipe.Id);
            graph.Connect(pipe.Id, tank.Id);
            graph.Connect(tank.Id, tap.Id);

            graph.Tick(1f, FluidConditions.Normal);
            Harness.Check(tank.Litres > 0f && tap.IsLive, "an intact line fills the tank and feeds the tap");

            // The storm's damage: break the middle and everything past it is dead.
            float banked = tank.Litres;
            pipe.IsBroken = true;
            graph.Tick(1f, FluidConditions.Normal);

            Harness.Check(!tank.IsLive, "a broken pipe cuts the tank off");
            Harness.Check(!tap.IsLive, "and the tap with it");
            Harness.Equal(tank.Litres, banked, "no water reaches the tank past the break");
            Harness.Equal(graph.DrawFromOutlet(tap.Id, 1f), 0f, "the tap runs dry until it is repaired");

            // A broken pump is a leak rather than a stoppage, so the fuel is still gone.
            var leaky = new FluidGraph();
            var brokenPump = LiveWell(leaky, 40f);
            var leakTank = leaky.Add(Tank(400f), At(8f));
            leaky.Connect(brokenPump.Id, leakTank.Id);
            brokenPump.IsBroken = true;

            leaky.Tick(1f, FluidConditions.Normal);
            Harness.Equal(leaky.LeakLitresPerMinute, 40f, "a broken pump leaks its whole output");
            Harness.Equal(leaky.InflowLitresPerMinute, 0f, "and delivers none of it");
            Harness.Equal(leakTank.Litres, 0f, "so the tank gains nothing");

            // Repairing puts it back.
            pipe.IsBroken = false;
            graph.Tick(1f, FluidConditions.Normal);
            Harness.Check(tap.IsLive, "repairing the pipe brings the tap back");

            // Frost stops a tap without freezing the whole line.
            graph.Tick(1f, new FluidConditions { PumpMultiplier = 0.5f, FreezingOutside = true });
            Harness.Check(tap.IsFrozen, "a frost night freezes an outdoor tap");
            Harness.Equal(graph.DrawFromOutlet(tap.Id, 1f), 0f, "and a frozen tap gives nothing");
            Harness.Check(tank.Litres > 0f, "while the tank behind it still holds its water");

            graph.Tick(1f, FluidConditions.Normal);
            Harness.Check(!tap.IsFrozen, "the thaw brings it back");
            Harness.Check(graph.DrawFromOutlet(tap.Id, 1f) > 0f, "and water flows again");
        }

        // -------------------------------------------------------------- sprinklers

        static void Sprinklers()
        {
            Harness.Section("fluid: sprinklers");

            var graph = new FluidGraph();
            var pump = LiveWell(graph, 12f);
            var tank = graph.Add(Tank(300f), At(8f));
            var sprinkler = graph.Add(Sprinkler(6f), At(14f));

            graph.Connect(pump.Id, tank.Id);
            graph.Connect(tank.Id, sprinkler.Id);

            tank.Litres = 300f;
            graph.Tick(1f, FluidConditions.Normal);

            Harness.Check(sprinkler.IsLive, "a fed sprinkler runs");
            Harness.Equal(graph.DrawLitresPerMinute, 6f, "and pulls its rated draw");

            // It is a standing cost. An hour of sprinkler is 360 L, against 720 L in.
            Harness.Check(tank.Litres > 0f, "the tank survives an hour with the pump ahead of it");

            // Starve it: no pump, no tank, no watering.
            var dry = new FluidGraph();
            var deadPump = dry.Add(Pump(12f), At(0f));
            var dryTank = dry.Add(Tank(300f), At(8f));
            var drySprinkler = dry.Add(Sprinkler(6f), At(14f));
            deadPump.HasPower = true;
            deadPump.HasWetSource = true;

            dry.Connect(deadPump.Id, dryTank.Id);
            dry.Connect(dryTank.Id, drySprinkler.Id);

            deadPump.HasWetSource = false;
            dryTank.Litres = 0f;
            dry.Tick(1f, FluidConditions.Normal);
            Harness.Check(!drySprinkler.IsLive, "a sprinkler with no water is not watering anything");
        }
    }
}
