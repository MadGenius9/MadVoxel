using MadVoxel.Power;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The grid, solved without a scene. These are the rules the whole electricity
    /// pass rests on: reach comes from relays, a brown-out is predictable, and fuel
    /// plus dark plus a flat battery is a dead grid.
    /// </summary>
    public static class PowerTests
    {
        public static void Run()
        {
            Wiring();
            Sources();
            RelaysAndSwitches();
            BrownOut();
            Batteries();
            Traps();
        }

        // ----------------------------------------------------------------- helpers

        static PowerDeviceDefinition Device(PowerDeviceKind kind, string id)
        {
            var def = ScriptableObject.CreateInstance<PowerDeviceDefinition>();
            def.stringId = id;
            def.displayName = id;
            def.kind = kind;
            def.maxWireLength = 12f;
            def.maxOutputs = 1;
            return def;
        }

        static PowerDeviceDefinition Generator(float watts, float fuelPerHour = 1.5f)
        {
            var def = Device(PowerDeviceKind.Generator, "test:gen");
            def.wattsProduced = watts;
            def.fuelLitresPerHour = fuelPerHour;
            def.fuelCapacityLitres = 20f;
            def.maxOutputs = 4;
            return def;
        }

        static PowerDeviceDefinition Solar(float watts)
        {
            var def = Device(PowerDeviceKind.SolarBank, "test:solar");
            def.wattsProduced = watts;
            def.maxOutputs = 4;
            return def;
        }

        static PowerDeviceDefinition Battery(float wattHours, float discharge = 60f)
        {
            var def = Device(PowerDeviceKind.BatteryBank, "test:battery");
            def.storageWattHours = wattHours;
            def.dischargeWatts = discharge;
            def.maxOutputs = 4;
            return def;
        }

        static PowerDeviceDefinition Relay(float range, float upkeep = 1f)
        {
            var def = Device(PowerDeviceKind.Relay, "test:relay");
            def.relayRange = range;
            def.relayUpkeepWatts = upkeep;
            def.maxOutputs = 4;
            return def;
        }

        static PowerDeviceDefinition Light(float watts)
        {
            var def = Device(PowerDeviceKind.Consumer, "test:light");
            def.wattsConsumed = watts;
            return def;
        }

        static Vector3 At(float x) { return new Vector3(x, 0f, 0f); }

        // ----------------------------------------------------------------- wiring

        static void Wiring()
        {
            Harness.Section("power: wiring");

            var graph = new PowerGraph();
            var gen = graph.Add(Generator(100f), At(0f));
            var near = graph.Add(Light(10f), At(8f));
            var far = graph.Add(Light(10f), At(40f));

            Harness.Equal(graph.Connect(gen.Id, near.Id), WireResult.Ok, "a wire inside range connects");
            Harness.Equal(graph.Connect(gen.Id, far.Id), WireResult.TooFar,
                "a wire past the device's reach is refused - this is what relays are for");

            Harness.Equal(graph.Connect(gen.Id, near.Id), WireResult.AlreadyWired, "the same wire twice is refused");
            Harness.Equal(graph.Connect(gen.Id, gen.Id), WireResult.SameDevice, "a device cannot wire to itself");
            Harness.Equal(graph.Connect(near.Id, gen.Id), WireResult.NoOutputSocket,
                "a consumer has no power out");

            var solar = graph.Add(Solar(40f), At(4f));
            Harness.Equal(graph.Connect(gen.Id, solar.Id), WireResult.NoInputSocket, "a source has no power in");

            // One output per device unless it is a splitter, so branching is a decision.
            var second = graph.Add(Light(10f), At(-6f));
            var single = graph.Add(Relay(30f), At(2f));
            single.Definition.maxOutputs = 1;
            graph.Connect(single.Id, near.Id);
            Harness.Equal(graph.Connect(single.Id, second.Id), WireResult.OutputsFull,
                "a full output socket needs a splitter");

            // Rings buy nothing without logic gates and make a brown-out ambiguous.
            var loopA = graph.Add(Relay(30f), At(100f));
            var loopB = graph.Add(Relay(30f), At(104f));
            graph.Connect(loopA.Id, loopB.Id);
            Harness.Equal(graph.Connect(loopB.Id, loopA.Id), WireResult.WouldLoop, "a ring is refused");

            // Removing a device must clean both ends, or the walk hits a hole.
            graph.Remove(near.Id);
            Harness.Check(!gen.Outputs.Contains(near.Id), "removing a device clears the wires into it");
            Harness.Equal(graph.Get(near.Id), null, "and the device is gone");

            for (int i = 0; i < 9; i++)
            {
                var verdict = (WireResult)i;
                string text = PowerGraph.Describe(verdict);
                Harness.Check(text != null && (verdict == WireResult.Ok || text.Length > 0),
                    "every wiring refusal has wording: " + verdict);
            }
        }

        // ---------------------------------------------------------------- sources

        static void Sources()
        {
            Harness.Section("power: sources");

            var graph = new PowerGraph();
            var gen = graph.Add(Generator(100f, 2f), At(0f));
            var light = graph.Add(Light(20f), At(6f));
            graph.Connect(gen.Id, light.Id);

            gen.FuelLitres = 10f;
            graph.Tick(1f, PowerConditions.Daylight);

            Harness.Check(light.IsPowered, "a fuelled generator lights the lamp");
            Harness.Equal(graph.SupplyWatts, 100f, "supply is the generator's output");
            Harness.Equal(graph.DemandWatts, 20f, "demand is what the lamp asked for");

            // Burn is proportional to load, with a floor: an idling engine is not free,
            // but running one lamp must not drink like running the farm.
            Harness.Check(gen.FuelLitres < 10f, "the generator burned fuel");
            Harness.Check(gen.FuelLitres > 10f - 2f, "and burned less than full-load fuel for a 20% load");

            gen.FuelLitres = 0f;
            graph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(!light.IsPowered, "an empty tank is a dead grid");
            Harness.Equal(graph.SupplyWatts, 0f, "and no supply at all");

            gen.FuelLitres = 10f;
            gen.SwitchedOn = false;
            graph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(!light.IsPowered, "a generator switched off produces nothing");

            // Solar is day only, and the weather gets a veto.
            var solarGraph = new PowerGraph();
            var solar = solarGraph.Add(Solar(50f), At(0f));
            var solarLight = solarGraph.Add(Light(20f), At(6f));
            solarGraph.Connect(solar.Id, solarLight.Id);

            solarGraph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(solarLight.IsPowered, "solar carries the lamp by day");

            solarGraph.Tick(1f, new PowerConditions { IsDay = false, SolarMultiplier = 1f });
            Harness.Check(!solarLight.IsPowered, "and nothing at night");

            solarGraph.Tick(1f, new PowerConditions { IsDay = true, SolarMultiplier = 0f });
            Harness.Check(!solarLight.IsPowered, "a storm takes the panel to zero even at noon");

            solarGraph.Tick(1f, new PowerConditions { IsDay = true, SolarMultiplier = 0.5f });
            Harness.Equal(solarGraph.SupplyWatts, 25f, "overcast halves the panel");
        }

        // ------------------------------------------------------- relays, switches

        static void RelaysAndSwitches()
        {
            Harness.Section("power: relays and switches");

            var graph = new PowerGraph();
            var gen = graph.Add(Generator(100f), At(0f));
            var relay = graph.Add(Relay(30f, 2f), At(10f));
            var light = graph.Add(Light(20f), At(36f));

            gen.FuelLitres = 10f;

            Harness.Equal(graph.Connect(gen.Id, relay.Id), WireResult.Ok, "the generator reaches the relay");
            Harness.Equal(graph.Connect(relay.Id, light.Id), WireResult.Ok,
                "and the relay reaches 26 m on, which the generator could not");

            graph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(light.IsPowered, "power arrives through the relay");
            Harness.Equal(graph.DemandWatts, 22f, "and the relay's own upkeep is on the bill");

            // A relay is a wall when it is off. So is a switch - that is the blackout.
            relay.SwitchedOn = false;
            graph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(!light.IsPowered, "a relay switched off cuts everything past it");
            Harness.Equal(graph.DemandWatts, 0f, "and nothing downstream is even billed");

            relay.SwitchedOn = true;

            var switchGraph = new PowerGraph();
            var sgen = switchGraph.Add(Generator(100f), At(0f));
            var breaker = switchGraph.Add(Device(PowerDeviceKind.Switch, "test:switch"), At(4f));
            breaker.Definition.maxOutputs = 4;
            var lamp = switchGraph.Add(Light(20f), At(8f));

            sgen.FuelLitres = 10f;
            switchGraph.Connect(sgen.Id, breaker.Id);
            switchGraph.Connect(breaker.Id, lamp.Id);

            switchGraph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(lamp.IsPowered, "a closed switch passes power");

            breaker.SwitchedOn = false;
            switchGraph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(!lamp.IsPowered, "the blackout switch is one click");
        }

        // --------------------------------------------------------------- brown-out

        static void BrownOut()
        {
            Harness.Section("power: brown-out");

            // 30 W of generator against 60 W of lamps. The near lamp must win, every
            // time, or the player cannot reason about what to unplug.
            var graph = new PowerGraph();
            var gen = graph.Add(Generator(30f), At(0f));
            var relay = graph.Add(Relay(30f, 0f), At(6f));
            var near = graph.Add(Light(20f), At(4f));
            var far = graph.Add(Light(20f), At(20f));
            var farther = graph.Add(Light(20f), At(26f));

            gen.FuelLitres = 10f;
            relay.Definition.maxOutputs = 4;

            graph.Connect(gen.Id, near.Id);
            graph.Connect(gen.Id, relay.Id);
            graph.Connect(relay.Id, far.Id);
            graph.Connect(relay.Id, farther.Id);

            graph.Tick(1f, PowerConditions.Daylight);

            Harness.Check(graph.IsBrownOut, "the grid reports the brown-out rather than hiding it");
            Harness.Check(near.IsPowered, "the lamp one hop from the generator stays lit");
            Harness.Check(!far.IsPowered && !farther.IsPowered, "the lamps further out go dark");
            Harness.Equal(graph.DemandWatts, 60f, "demand still reports what was asked for");

            // And it is stable: the same grid browns out the same way twice.
            graph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(near.IsPowered && !far.IsPowered, "the same devices lose power on the next tick");
        }

        // --------------------------------------------------------------- batteries

        static void Batteries()
        {
            Harness.Section("power: batteries");

            var graph = new PowerGraph();
            var solar = graph.Add(Solar(60f), At(0f));
            var bank = graph.Add(Battery(100f, 60f), At(4f));
            var pump = graph.Add(Light(20f), At(8f));
            pump.Definition.displayName = "pump";

            graph.Connect(solar.Id, bank.Id);
            graph.Connect(bank.Id, pump.Id);

            // Day: the panel runs the pump and banks the surplus.
            graph.Tick(1f, PowerConditions.Daylight);
            Harness.Check(pump.IsPowered, "the panel runs the pump by day");
            Harness.Check(bank.StoredWattHours > 0f, "and the surplus goes into the bank");
            Harness.Equal(bank.StoredWattHours, 40f, "40 W of surplus for an hour is 40 Wh");

            // Night: the bank carries it.
            graph.Tick(1f, new PowerConditions { IsDay = false, SolarMultiplier = 1f });
            Harness.Check(pump.IsPowered, "the bank keeps the pump alive after dark");
            Harness.Equal(bank.StoredWattHours, 20f, "and pays 20 Wh for the hour");

            // ...until it is empty. Then the tap is dry.
            graph.Tick(1f, new PowerConditions { IsDay = false, SolarMultiplier = 1f });
            Harness.Check(pump.IsPowered, "the last hour in the bank still counts");
            Harness.Equal(bank.StoredWattHours, 0f, "the bank is now flat");

            graph.Tick(1f, new PowerConditions { IsDay = false, SolarMultiplier = 1f });
            Harness.Check(!pump.IsPowered, "dark plus a flat bank is a dead grid");

            // A bank cannot pay out faster than its discharge rating, however full it is.
            var fast = new PowerGraph();
            var slowBank = fast.Add(Battery(500f, 10f), At(0f));
            var hungry = fast.Add(Light(50f), At(4f));
            fast.Connect(slowBank.Id, hungry.Id);
            slowBank.StoredWattHours = 500f;

            fast.Tick(1f, new PowerConditions { IsDay = false, SolarMultiplier = 1f });
            Harness.Check(!hungry.IsPowered, "a full bank with a small inverter still cannot run a big draw");
            Harness.Equal(fast.BatteryWatts, 10f, "it offers only its discharge rating");
        }

        // ------------------------------------------------------------------- traps

        static void Traps()
        {
            Harness.Section("power: traps idle cheap");

            var trapDef = Device(PowerDeviceKind.Consumer, "test:blade");
            trapDef.drawMode = PowerDrawMode.IdleThenBurst;
            trapDef.idleWatts = 2f;
            trapDef.wattsConsumed = 40f;
            trapDef.trapDamage = 18f;

            var graph = new PowerGraph();
            var gen = graph.Add(Generator(60f), At(0f));
            var trap = graph.Add(trapDef, At(6f));
            gen.FuelLitres = 10f;
            graph.Connect(gen.Id, trap.Id);

            graph.Tick(1f, PowerConditions.Daylight);
            Harness.Equal(graph.DemandWatts, 2f, "an armed trap with nothing in front of it sips");
            Harness.Check(trap.IsPowered, "and is armed");

            // This is the point of the idle draw: a small generator can arm a trap it
            // could never actually run flat out, so defending costs you on the night.
            trap.Triggered = true;
            graph.Tick(1f, PowerConditions.Daylight);
            Harness.Equal(graph.DemandWatts, 40f, "a spinning trap costs real watts");
            Harness.Check(trap.IsPowered, "and a 60 W generator swings it");

            var small = new PowerGraph();
            var tinyGen = small.Add(Generator(10f), At(0f));
            var bigTrap = small.Add(trapDef, At(6f));
            tinyGen.FuelLitres = 10f;
            small.Connect(tinyGen.Id, bigTrap.Id);

            small.Tick(1f, PowerConditions.Daylight);
            Harness.Check(bigTrap.IsPowered, "a tiny generator can still arm it");

            bigTrap.Triggered = true;
            small.Tick(1f, PowerConditions.Daylight);
            Harness.Check(!bigTrap.IsPowered, "but cannot swing it when something walks in");
        }
    }
}
