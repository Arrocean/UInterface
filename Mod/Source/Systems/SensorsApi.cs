using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UBOAT.Game.Sandbox;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace UInterface
{
    public static class SensorsApi
    {
        public const int ApiVersion = 1;

        private sealed class SensorBinding
        {
            internal readonly object Provider;
            internal readonly Func<string> ReadState;

            internal SensorBinding(object provider, Func<string> readState)
            {
                Provider = provider;
                ReadState = readState;
            }
        }

        private static readonly ShipBindings<SensorBinding> Sensors = new ShipBindings<SensorBinding>(
            NotifyAvailabilityChanged, binding => IsProviderAlive(binding.Provider));
        private static bool monitoringAvailability;

        public static event Action<string> OnSensorStateChanged;
        public static event Action<string, bool> OnSensorAvailabilityChanged;

        public static IDisposable RegisterSensor(string equipmentId, object provider, Func<string> readState)
        {
            if (!IsProviderAlive(provider) || readState == null ||
                !SubmarineApi.TryGetEquipmentMount(equipmentId, out var mount))
            {
                return null;
            }

            var registration = Sensors.Register(equipmentId, new SensorBinding(provider, readState), mount);
            if (registration != null && !monitoringAvailability)
            {
                monitoringAvailability = true;
                MonitorAvailability().Forget();
            }

            return registration;
        }

        private static async UniTask MonitorAvailability()
        {
            try
            {
                while (Sensors.HasEntries)
                {
                    await UniTask.Delay(250, ignoreTimeScale: true);
                    Sensors.SweepUnavailable();
                }
            }
            finally
            {
                monitoringAvailability = false;
            }
        }

        public static string[] GetRegisteredSensorIds()
        {
            return Sensors.GetIds().Where(id => TryGetSensorBinding(id, out _)).ToArray();
        }

        public static bool TryGetSensor(string equipmentId, Type contract, out object provider)
        {
            provider = null;
            if (contract == null || !TryGetSensorBinding(equipmentId, out var binding) ||
                !contract.IsInstanceOfType(binding.Provider))
            {
                return false;
            }

            provider = binding.Provider;
            return true;
        }

        public static bool TryGetSensorState(string equipmentId, out string state)
        {
            state = null;
            if (!TryGetSensorBinding(equipmentId, out var binding))
            {
                return false;
            }

            string current = binding.ReadState();
            if (string.IsNullOrWhiteSpace(current))
            {
                return false;
            }

            state = current;
            return true;
        }

        public static bool NotifySensorStateChanged(string equipmentId, object provider)
        {
            if (!TryGetSensorBinding(equipmentId, out var binding) || !ReferenceEquals(binding.Provider, provider))
            {
                return false;
            }

            var handlers = OnSensorStateChanged;
            if (handlers != null)
            {
                foreach (Action<string> handler in handlers.GetInvocationList())
                {
                    if (!Sensors.TryGet(equipmentId, out var current) || current != binding)
                    {
                        break;
                    }

                    try
                    {
                        handler(equipmentId);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }

            return true;
        }

        private static bool TryGetSensorBinding(string equipmentId, out SensorBinding binding)
        {
            return Sensors.TryGet(equipmentId, out binding, out var mount) &&
                SubmarineApi.TryGetEquipmentMount(equipmentId, out var resolved) && resolved == mount;
        }

        private static bool IsProviderAlive(object provider)
        {
            return provider != null && (!(provider is UnityEngine.Object component) || component);
        }

        private static void NotifyAvailabilityChanged(string equipmentId, bool available)
        {
            var handlers = OnSensorAvailabilityChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<string, bool> handler in handlers.GetInvocationList())
            {
                if (TryGetSensorBinding(equipmentId, out _) != available)
                {
                    break;
                }

                try
                {
                    handler(equipmentId, available);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public static IDirectDetector[] GetNativeDetectors()
        {
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) || ship.Detectors == null)
            {
                return Array.Empty<IDirectDetector>();
            }

            return (IDirectDetector[])ship.Detectors.Clone();
        }

        public static DirectObservation[] GetNativeObservations()
        {
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return Array.Empty<DirectObservation>();
            }

            return ship.GetObservationsDirect()?.ToArray() ?? Array.Empty<DirectObservation>();
        }

        public static bool TryReportNativeObservation(string equipmentId, object provider, Entity target,
            DirectObservationType type, out DirectObservation observation)
        {
            observation = null;
            if ((type != DirectObservationType.Radar && type != DirectObservationType.RadarDetector &&
                    type != DirectObservationType.Hydrophone && type != DirectObservationType.Sonar) ||
                !SubmarineApi.TryGetReadySubmarine(out _, out var ship) || !target || target == ship ||
                ship.SandboxEntity == null || !ship.SandboxGroup ||
                target.SandboxEntity?.SpawnState != SandboxEntity.EntitySpawnState.Spawned ||
                !TryGetSensorBinding(equipmentId, out var binding) || !ReferenceEquals(binding.Provider, provider))
            {
                return false;
            }

            observation = ship.AddObservation(target, type);
            return observation != null;
        }

        public static bool TryGetRadio(out PlayerShipRadio radio)
        {
            radio = null;
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            radio = ship.Radio;
            return radio != null;
        }
    }
}
