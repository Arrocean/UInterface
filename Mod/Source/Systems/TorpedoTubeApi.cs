using System;
using UBOAT.Game.Scene.Items;

namespace UInterface
{
    public static class TorpedoTubeApi
    {
        public const int ApiVersion = 1;

        public static ExtendedTorpedoLauncher[] GetTorpedoTubes()
        {
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) || ship.TorpedoLaunchers == null)
            {
                return Array.Empty<ExtendedTorpedoLauncher>();
            }

            return (ExtendedTorpedoLauncher[])ship.TorpedoLaunchers.Clone();
        }
    }
}
