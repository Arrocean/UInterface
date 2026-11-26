using System;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Entities;
using UnityEngine;

namespace UInterface
{
    public static class PowerApi
    {
        public const int ApiVersion = 1;

        internal const string FeatureId = "uinterface.power-supply";
        private static readonly ShipBindings<Contribution> Contributions = new ShipBindings<Contribution>();
        private static readonly Func<Rates, Contribution, Rates> AddRate = Accumulate;

        public static IDisposable RegisterSource(PlayerShip ship, string id, float initialRate,
            out Func<float, bool> setRate, string mountId = null)
        {
            return Register(ship, id, initialRate, out setRate, mountId, isSource: true);
        }

        public static IDisposable RegisterLoad(PlayerShip ship, string id, float initialRate,
            out Func<float, bool> setRate, string mountId = null)
        {
            return Register(ship, id, initialRate, out setRate, mountId, isSource: false);
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
            out Func<float, bool> setRate, string mountId, bool isSource)
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

            var contribution = new Contribution(isSource, initialRate);
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
            else
            {
                rates.Consumption += contribution.Rate;
            }

            return rates;
        }

        private sealed class Contribution
        {
            internal readonly bool IsSource;
            internal float Rate;

            internal Contribution(bool isSource, float rate)
            {
                IsSource = isSource;
                Rate = rate;
            }
        }

        private struct Rates
        {
            internal double Generation;
            internal double Consumption;
        }
    }
}
