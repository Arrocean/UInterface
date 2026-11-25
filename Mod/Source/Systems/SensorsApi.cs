using System;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;

namespace UInterface
{
    public static class SensorsApi
    {
        public const int ApiVersion = 1;

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
