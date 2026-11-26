using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Physics;

namespace UInterface
{
    public static class EngineeringApi
    {
        public const int ApiVersion = 1;

        private static readonly FieldInfo TrimPumpTanks = AccessTools.Field(typeof(TrimPump), "tanks");

        public static bool RegisterNativePropulsionBridge(string submarineId, string surfaceEnginePath,
            string submergedEnginePath)
        {
            return RegisterNativePropulsionBinding(submarineId, surfaceEnginePath, submergedEnginePath);
        }

        public static bool RegisterNativePropulsionBinding(string submarineId, string surfaceEnginePath,
            string submergedEnginePath)
        {
            return NativePropulsionBinding.Register(submarineId, surfaceEnginePath, submergedEnginePath);
        }

        public static bool TryGetBallast(out Tank[] tanks, out bool flooding)
        {
            tanks = Array.Empty<Tank>();
            flooding = false;
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) || ship.BallastTanks == null)
            {
                return false;
            }

            tanks = (Tank[])ship.BallastTanks.Clone();
            flooding = ship.FloodBallastTanks;
            return tanks.Length != 0;
        }

        public static bool TrySetBallastFlooding(bool flooding)
        {
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) ||
                ship.BallastTanks == null || ship.BallastTanks.Length == 0)
            {
                return false;
            }

            ship.FloodBallastTanks = flooding;
            return true;
        }

        public static bool TryGetTrimPump(out TrimPump pump)
        {
            return ComponentApi.TryGetUnique(out pump);
        }

        public static TrimPump[] GetTrimPumps(bool includeInactive = false)
        {
            return ComponentApi.GetShipComponents(typeof(TrimPump), includeInactive).Cast<TrimPump>().ToArray();
        }

        public static bool TryGetTrimPumpTanks(TrimPump pump, out Tank[] tanks)
        {
            tanks = Array.Empty<Tank>();
            if (!pump || !SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) || pump.ParentEntity != ship ||
                !(TrimPumpTanks?.GetValue(pump) is TrimPump.ConnectedTank[] connections))
            {
                return false;
            }

            tanks = connections.Where(connection => connection != null && connection.Tank)
                .Select(connection => connection.Tank).Distinct().ToArray();
            return true;
        }

        public static bool TryGetPropulsion(out PlayerShipEngine diesel, out PlayerShipEngine electric)
        {
            diesel = null;
            electric = null;
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            diesel = ship.DieselEngine;
            electric = ship.ElectricEngine;
            return diesel || electric;
        }

        public static bool TryRequestStationaryCharging()
        {
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) ||
                !ship.DieselEngine || !ship.ElectricEngine || !ship.ActiveEngine)
            {
                return false;
            }

            ship.OrderEngineGear(0, loadBattery: true);
            return true;
        }

        public static bool TryGetResources(out Resource energy, out Resource fuel, out Resource oxygen)
        {
            energy = null;
            fuel = null;
            oxygen = null;
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            energy = ship.Energy;
            fuel = ship.Fuel;
            oxygen = ship.Oxygen;
            return energy != null || fuel != null || oxygen != null;
        }

        public static Compartment[] GetCompartments()
        {
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) ||
                !ship.Interior || ship.Interior.Compartments == null)
            {
                return Array.Empty<Compartment>();
            }

            return (Compartment[])ship.Interior.Compartments.Clone();
        }
    }
}
