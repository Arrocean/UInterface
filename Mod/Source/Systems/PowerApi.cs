using System;
using Cysharp.Threading.Tasks;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace UInterface
{
    public static class PowerApi
    {
        public const int ApiVersion = 1;

        internal const string FeatureId = "uinterface.power-supply";
        private static readonly ShipBindings<Contribution> Contributions = new ShipBindings<Contribution>(
            isAlive: contribution => contribution.Interlock == null || contribution.Interlock.IsAlive,
            onRemoved: contribution => contribution.Interlock?.Disconnect());
        private static readonly Func<Rates, Contribution, Rates> AddRate = Accumulate;
        private static Resource monitoredBattery;
        private static bool refreshPending;

        static PowerApi()
        {
            PlayerSubmarineContext.Invalidated += StopMonitoringBattery;
        }

        public static IDisposable RegisterSource(PlayerShip ship, string id, float initialRate,
            out Func<float, bool> setRate, string mountId = null)
        {
            return Register(ship, id, initialRate, out setRate, mountId, isSource: true, interlock: null);
        }

        public static IDisposable RegisterLoad(PlayerShip ship, string id, float initialRate,
            out Func<float, bool> setRate, string mountId = null)
        {
            return Register(ship, id, initialRate, out setRate, mountId, isSource: false, interlock: null);
        }

        public static IDisposable RegisterInterlockedLoad(PlayerShip ship, string id, float initialRate,
            Compartment compartment, Action<bool> powerChanged, out Func<float, bool> setRate, string mountId = null)
        {
            setRate = null;
            if (!ship || !compartment || compartment.ParentEntity != ship || ship.Energy == null ||
                !SubmarineRegistry.IsIdentifier(id) || !IsValidRate(initialRate) ||
                !SubmarineApi.TryGetReadySubmarine(out _, out var currentShip) || currentShip != ship ||
                !SubmarineFramework.TryInstallPowerInterlocks())
            {
                return null;
            }

            var interlock = new CompartmentInterlock(compartment, ship.Energy, powerChanged);
            var registration = Register(ship, id, initialRate, out setRate, mountId, isSource: false, interlock: interlock);
            if (registration != null)
            {
                MonitorBattery(ship.Energy);
                ScheduleInterlockRefresh();
            }

            return registration;
        }

        public static bool TryGetLoadPower(string id, out bool powered)
        {
            powered = false;
            if (!Contributions.TryGet(id, out var contribution) || contribution.Interlock == null)
            {
                return false;
            }

            powered = contribution.Interlock.Powered;
            return true;
        }

        public static bool TryGetBattery(out double charge, out float capacity)
        {
            charge = 0d;
            capacity = 0f;
            if (!SubmarineApi.TryGetReadySubmarine(out _, out var ship) || ship.Energy == null)
            {
                return false;
            }

            charge = ship.Energy.Amount;
            capacity = ship.Energy.Capacity.Value;
            return true;
        }

        public static bool TryGetCustomPowerRates(out double generation, out double consumption)
        {
            generation = 0d;
            consumption = 0d;
            if (!SubmarineApi.TryGetReadySubmarine(out _, out var ship) || ship.Energy == null)
            {
                return false;
            }

            var rates = Contributions.AggregateAvailable(default(Rates), AddRate);
            generation = rates.Generation;
            consumption = rates.Consumption;
            return true;
        }

        internal static double ResolveSandboxGain(Parameter gain, Resource resource)
        {
            double nativeGain = gain;
            var ship = PlayerSubmarineContext.Ship;
            if (!Contributions.HasEntries || !ship || !ReferenceEquals(ship.Energy, resource))
            {
                return nativeGain;
            }

            var rates = Contributions.AggregateAvailable(default(Rates), AddRate);
            return nativeGain + (rates.Generation - rates.Consumption);
        }

        private static IDisposable Register(PlayerShip ship, string id, float initialRate,
            out Func<float, bool> setRate, string mountId, bool isSource, CompartmentInterlock interlock)
        {
            setRate = null;
            if (!ship || !SubmarineRegistry.IsIdentifier(id) || !IsValidRate(initialRate) ||
                !SubmarineApi.TryGetReadySubmarine(out _, out var currentShip) || currentShip != ship || ship.Energy == null)
            {
                return null;
            }

            Transform mount = null;
            if ((mountId != null && !SubmarineApi.TryGetMount(mountId, out mount)) ||
                !SubmarineFramework.TryInstallPowerSupply())
            {
                return null;
            }

            var contribution = new Contribution(isSource, initialRate, interlock);
            var registration = Contributions.Register(id, contribution, mount);
            if (registration != null)
            {
                setRate = rate =>
                {
                    if (!IsValidRate(rate) || !Contributions.TryGet(id, out var current) || current != contribution)
                    {
                        return false;
                    }

                    contribution.Rate = rate;
                    return true;
                };
            }

            return registration;
        }

        private static bool IsValidRate(float rate)
        {
            return rate >= 0f && !float.IsInfinity(rate);
        }

        private static Rates Accumulate(Rates rates, Contribution contribution)
        {
            if (contribution.IsSource)
            {
                rates.Generation += contribution.Rate;
            }
            else if (contribution.Interlock == null || contribution.Interlock.Powered)
            {
                rates.Consumption += contribution.Rate;
            }

            return rates;
        }

        internal static void OnCompartmentPowerChanged(Compartment compartment)
        {
            if (monitoredBattery != null && compartment && compartment.ParentEntity == PlayerSubmarineContext.Ship)
            {
                ScheduleInterlockRefresh();
            }
        }

        private static void MonitorBattery(Resource battery)
        {
            if (monitoredBattery != battery)
            {
                StopMonitoringBattery();
                monitoredBattery = battery;
                monitoredBattery.DepletionStateChanged += OnBatteryDepletionChanged;
            }
        }

        private static void StopMonitoringBattery()
        {
            if (monitoredBattery != null)
            {
                monitoredBattery.DepletionStateChanged -= OnBatteryDepletionChanged;
                monitoredBattery = null;
            }
        }

        private static void OnBatteryDepletionChanged(Resource battery)
        {
            ScheduleInterlockRefresh();
        }

        private static void ScheduleInterlockRefresh()
        {
            if (!refreshPending)
            {
                refreshPending = true;
                RefreshInterlocks().Forget();
            }
        }

        private static async UniTask RefreshInterlocks()
        {
            // Native battery and board callbacks can update several compartments in one frame.
            await UniTask.NextFrame();
            refreshPending = false;
            foreach (string id in Contributions.GetIds())
            {
                if (Contributions.TryGet(id, out var contribution))
                {
                    contribution.Interlock?.Refresh();
                }
            }
        }

        private sealed class Contribution
        {
            internal readonly bool IsSource;
            internal readonly CompartmentInterlock Interlock;
            internal float Rate;

            internal Contribution(bool isSource, float rate, CompartmentInterlock interlock)
            {
                IsSource = isSource;
                Rate = rate;
                Interlock = interlock;
            }
        }

        private sealed class CompartmentInterlock
        {
            private readonly Compartment compartment;
            private readonly Resource battery;
            private readonly Action<bool> powerChanged;
            private bool? reportedPower;

            internal bool IsAlive => compartment;
            internal bool Powered => compartment && compartment.ElectricityEnabled && battery.Amount > 0d;

            internal CompartmentInterlock(Compartment compartment, Resource battery, Action<bool> powerChanged)
            {
                this.compartment = compartment;
                this.battery = battery;
                this.powerChanged = powerChanged;
            }

            internal void Refresh()
            {
                Report(Powered);
            }

            internal void Disconnect()
            {
                if (reportedPower == true)
                {
                    Report(false);
                }
            }

            private void Report(bool powered)
            {
                if (reportedPower != powered)
                {
                    reportedPower = powered;
                    try
                    {
                        powerChanged?.Invoke(powered);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
        }

        private struct Rates
        {
            internal double Generation;
            internal double Consumption;
        }
    }
}
