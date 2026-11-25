using System;
using Cysharp.Threading.Tasks;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Skills;
using ShipActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace UInterface
{
    public static class StationApi
    {
        public const int ApiVersion = 1;

        public static bool RegisterStation(string submarineId, string stationId, string mountId)
        {
            return StationRegistry.RegisterStation(submarineId, stationId, mountId);
        }

        public static bool RegisterAction(string stationId, string actionId, Type actionType)
        {
            return StationRegistry.RegisterAction(stationId, actionId, actionType);
        }

        public static bool TryGetStation(string stationId, out ShipActivator station)
        {
            return UboatStationAdapter.TryGetStation(stationId, out station);
        }


        public static ShipActivator[] GetStations(string stationId)
        {
            return UboatStationAdapter.GetStations(stationId);
        }

        public static CharacterActionInfo[] QueryNativeActions(ShipActivator station, PlayableCharacter actor)
        {
            return UboatStationAdapter.QueryNativeActions(station, actor);
        }

        public static UniTask<bool> RequestNativeAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            return UboatStationAdapter.RequestNativeAction(station, template, actor);
        }

        public static string[] QueryActions(string stationId, PlayableCharacter actor)
        {
            if (!actor || !TryGetStation(stationId, out var station))
            {
                return Array.Empty<string>();
            }

            return UboatStationAdapter.QueryActions(stationId, station, actor);
        }

        public static bool TryGetActionState(string stationId, string actionId, PlayableCharacter actor,
            out ActionState state)
        {
            return UboatStationAdapter.TryGetAction(stationId, actionId, actor, out _, out _, out state);
        }

        public static async UniTask<bool> RequestAction(string stationId, string actionId, PlayableCharacter actor)
        {
            if (!UboatStationAdapter.TryGetAction(stationId, actionId, actor, out var station, out var template,
                    out ActionState state) || state != ActionState.Enabled)
            {
                return false;
            }

            return await UboatStationAdapter.StartAction(station, template, actor);
        }

        public static bool TryGetState(string stationId, out bool hasAssignedCrew, out bool switchedOn,
            out bool operating)
        {
            TryGetStation(stationId, out var station);
            return UboatStationAdapter.TryGetState(station, out hasAssignedCrew, out switchedOn, out operating);
        }

        public static IDisposable SubscribeStateChanged(string stationId, Action callback)
        {
            if (callback == null || !TryGetStation(stationId, out var station))
            {
                return null;
            }

            return new StationStateSubscription(station, callback);
        }
    }
}
