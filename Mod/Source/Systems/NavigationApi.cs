using System;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;

namespace UInterface
{
    public static class NavigationApi
    {
        public const int ApiVersion = 1;

        public static bool TryGetSteering(out NavigationTable navigationTable, out Rudder[] rudders)
        {
            navigationTable = null;
            rudders = Array.Empty<Rudder>();
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            navigationTable = ship.NavigationTable;
            if (ship.Rudders != null)
            {
                rudders = (Rudder[])ship.Rudders.Clone();
            }

            return navigationTable != null || rudders.Length != 0;
        }

        public static bool TrySetCompassPreset(CompassPreset preset)
        {
            if (!Enum.IsDefined(typeof(CompassPreset), preset) ||
                !SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            ship.SetCompassPreset(preset);
            return true;
        }

        public static bool TryGetDepth(out float depth, out float targetDepth, out DepthPreset preset)
        {
            depth = 0f;
            targetDepth = 0f;
            preset = DepthPreset.None;
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            depth = ship.Depth;
            targetDepth = ship.TargetDepth;
            preset = ship.DepthPreset;
            return true;
        }

        public static bool TrySetDepthPreset(DepthPreset preset)
        {
            if (!Enum.IsDefined(typeof(DepthPreset), preset) ||
                !SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            ship.SetDepthPreset(preset);
            return true;
        }

        public static bool TrySetTargetDepth(float depth)
        {
            if (float.IsNaN(depth) || float.IsInfinity(depth) || depth < 0f ||
                !SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            ship.SetTargetDepth(depth);
            return true;
        }
    }
}
