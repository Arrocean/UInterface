using System;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Items;

namespace UInterface
{
    public static class CrewServicesApi
    {
        public const int ApiVersion = 1;

        [Inject]
        private static PlayerCrew crew = null;

        public static PlayableCharacter[] GetCrew()
        {
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out _) || crew?.Characters == null)
            {
                return Array.Empty<PlayableCharacter>();
            }

            return (PlayableCharacter[])crew.Characters.Clone();
        }

        public static bool TryGetSupplies(out Storage main, out Storage accessories)
        {
            main = null;
            accessories = null;
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return false;
            }

            main = ship.MainStorage;
            accessories = ship.AccessoriesStorage;
            return main != null || accessories != null;
        }
    }
}
