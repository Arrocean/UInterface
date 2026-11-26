using System;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace UInterface
{
    public static class PropulsionApi
    {
        public const int ApiVersion = 1;

        internal const string FeatureId = "uinterface.propulsion-output";
        private static readonly ShipBindings<Provider> Providers = new ShipBindings<Provider>();

        public static IDisposable RegisterProvider(PlayerShip ship,
            Func<PlayerShipEngine, float, float> resolvePower,
            Func<PlayerShipEngine, float, float> resolveTargetSpeed = null)
        {
            if (!ship || (resolvePower == null && resolveTargetSpeed == null) ||
                !SubmarineApi.TryGetReadySubmarine(out _, out var currentShip) || currentShip != ship ||
                !SubmarineFramework.TryInstallPropulsionOutput())
            {
                return null;
            }

            return Providers.Register(FeatureId, new Provider(resolvePower, resolveTargetSpeed));
        }

        internal static float ResolvePower(Engines engines, Ship ship)
        {
            float nativePower = engines.Power.Value;
            if (!(engines is PlayerShipEngine engine) || !(ship is PlayerShip playerShip) ||
                !TryGetProvider(playerShip, out var provider) || provider.Power == null)
            {
                return nativePower;
            }

            if (!CanPropel(playerShip, engine))
            {
                return 0f;
            }

            if (!provider.Power.TryRead(engine, nativePower, out float power))
            {
                return nativePower;
            }

            float maxPower = engine.MaxPower;
            return Mathf.Clamp(power, -maxPower, maxPower);
        }

        internal static void SetTargetSpeed(Ship ship, float nativeSpeed)
        {
            float speed = nativeSpeed;
            if (ship is PlayerShip playerShip && TryGetProvider(playerShip, out var provider) &&
                provider.TargetSpeed != null)
            {
                var engine = playerShip.ActiveEngine;
                if (!CanPropel(playerShip, engine))
                {
                    speed = 0f;
                }
                else if (provider.TargetSpeed.TryRead(engine, nativeSpeed, out float targetSpeed) && targetSpeed >= 0f)
                {
                    speed = targetSpeed;
                }
            }

            ship.TargetVelocity = speed;
        }

        private static bool TryGetProvider(PlayerShip ship, out Provider provider)
        {
            provider = null;
            return Providers.HasEntries && ship == PlayerSubmarineContext.Ship &&
                Providers.TryGet(FeatureId, out provider);
        }

        private static bool CanPropel(PlayerShip ship, PlayerShipEngine engine)
        {
            return engine && engine.isActiveAndEnabled && engine.GearIndex != 0 &&
                !engine.GearsBlockade && !engine.LoadBatteryStationary && !ship.Docked &&
                engine.Performance.Value > 0.1f;
        }

        private sealed class Provider
        {
            internal readonly Output Power;
            internal readonly Output TargetSpeed;

            internal Provider(Func<PlayerShipEngine, float, float> power, Func<PlayerShipEngine, float, float> targetSpeed)
            {
                Power = power == null ? null : new Output(power, "propeller power");
                TargetSpeed = targetSpeed == null ? null : new Output(targetSpeed, "target speed");
            }
        }

        private sealed class Output
        {
            private readonly Func<PlayerShipEngine, float, float> read;
            private readonly string name;
            private bool reportedFailure;

            internal Output(Func<PlayerShipEngine, float, float> read, string name)
            {
                this.read = read;
                this.name = name;
            }

            internal bool TryRead(PlayerShipEngine engine, float nativeValue, out float value)
            {
                try
                {
                    value = read(engine, nativeValue);
                    return !float.IsNaN(value) && !float.IsInfinity(value);
                }
                catch (Exception exception)
                {
                    if (!reportedFailure)
                    {
                        reportedFailure = true;
                        Debug.LogError("[UInterface] Failed to read custom " + name + ". Using the native value.");
                        Debug.LogException(exception);
                    }

                    value = nativeValue;
                    return false;
                }
            }
        }
    }
}
