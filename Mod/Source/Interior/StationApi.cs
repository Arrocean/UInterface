using System;
using System.Linq;
using System.Runtime.CompilerServices;
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

        private static readonly ConditionalWeakTable<CharacterAction, PlayableCharacter> TrackedActions =
            new ConditionalWeakTable<CharacterAction, PlayableCharacter>();

        private static readonly ConditionalWeakTable<CharacterAction, ActionQueue> QueuedActions =
            new ConditionalWeakTable<CharacterAction, ActionQueue>();

        static StationApi()
        {
            PlayerSubmarineContext.Invalidated += TrackedActions.Clear;
            PlayerSubmarineContext.Invalidated += QueuedActions.Clear;
        }

        public static bool RegisterStation(string submarineId, string stationId, string mountId)
        {
            return StationRegistry.RegisterStation(submarineId, stationId, mountId);
        }

        public static bool RegisterStationGroup(string submarineId, string stationId, string[] mountIds)
        {
            return StationRegistry.RegisterStationGroup(submarineId, stationId, mountIds);
        }

        public static bool RegisterAction(string stationId, string actionId, Type actionType)
        {
            return StationRegistry.RegisterAction(stationId, actionId, actionType);
        }

        public static bool RegisterActionSelector(string stationId, string actionId, Type actionType,
            Func<CharacterAction, bool> selector)
        {
            return selector != null && StationRegistry.RegisterAction(stationId, actionId, actionType, selector);
        }

        public static bool RegisterSubmarineAction(string submarineId, string stationId, string actionId,
            Type actionType, Func<CharacterAction, bool> selector = null)
        {
            return StationRegistry.RegisterSubmarineAction(submarineId, stationId, actionId, actionType, selector);
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

        public static CharacterActionInfo[] QueryNativeUtilityActions(ShipActivator station, PlayableCharacter actor)
        {
            return UboatStationAdapter.QueryNativeUtilityActions(station, actor);
        }

        public static CharacterActionInfo[] QueryQueuedNativeActions(ShipActivator station, PlayableCharacter actor)
        {
            return UboatStationAdapter.QueryQueuedNativeActions(station, actor);
        }

        public static CharacterAction[] GetActiveQueuedNativeActions(PlayableCharacter actor)
        {
            var queue = PlayerSubmarineReadiness.IsReady ? UboatStationAdapter.GetActionQueue(actor) : null;
            return queue == null ? Array.Empty<CharacterAction>() : queue.Actions.Where(IsTrackableQueuedAction).ToArray();
        }

        public static bool TryTrackQueuedNativeAction(PlayableCharacter actor, CharacterAction action)
        {
            var queue = PlayerSubmarineReadiness.IsReady ? UboatStationAdapter.GetActionQueue(actor) : null;
            if (queue == null || !IsTrackableQueuedAction(action) || !queue.Contains(action))
            {
                return false;
            }

            QueuedActions.Remove(action);
            QueuedActions.Add(action, queue);
            return true;
        }

        private static bool IsTrackableQueuedAction(CharacterAction action)
        {
            return action != null && action.IsDirectPlayerOrder && action.Stage != CharacterActionStage.Completed &&
                UboatStationAdapter.IsCurrentStation(action.Activator, requireActive: false);
        }

        public static async UniTask<CharacterAction> RequestQueuedNativeAction(ShipActivator station,
            CharacterAction template, PlayableCharacter actor)
        {
            var queue = UboatStationAdapter.GetActionQueue(actor);
            int revision = PlayerSubmarineContext.Revision;
            var action = await UboatStationAdapter.QueueNativeAction(station, template, actor, queue);
            if (action == null || revision != PlayerSubmarineContext.Revision ||
                !UboatStationAdapter.IsCurrentStation(station, requireActive: false))
            {
                return null;
            }

            QueuedActions.Add(action, queue);
            return action;
        }

        public static bool TryCancelQueuedNativeAction(CharacterAction action)
        {
            if (action == null || !PlayerSubmarineReadiness.CanBind || !QueuedActions.TryGetValue(action, out var queue) ||
                !UboatStationAdapter.IsCurrentStation(action.Activator, requireActive: false) ||
                !action.CanBeStopped || action.Stage == CharacterActionStage.Completed || !queue.Contains(action))
            {
                return false;
            }

            queue.Remove(action);
            return true;
        }

        public static UniTask<bool> RequestNativeUtilityAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            return UboatStationAdapter.RequestNativeUtilityAction(station, template, actor);
        }

        public static async UniTask<bool> RequestNativeAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            return UboatStationAdapter.CanRequestNativeAction(station, template, actor) &&
                (await UboatStationAdapter.StartAction(station, template, actor)).Accepted;
        }

        public static UniTask<CharacterAction> RequestTrackedNativeAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            return UboatStationAdapter.CanRequestNativeAction(station, template, actor)
                ? StartTrackedAction(station, template, actor) : UniTask.FromResult<CharacterAction>(null);
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

        public static string[] QueryStationActions(string stationId, ShipActivator station, PlayableCharacter actor)
        {
            return actor && IsStationInGroup(stationId, station)
                ? UboatStationAdapter.QueryActions(stationId, station, actor) : Array.Empty<string>();
        }

        public static bool TryGetStationActionState(string stationId, ShipActivator station, string actionId,
            PlayableCharacter actor, out ActionState state)
        {
            state = ActionState.Hidden;
            return IsStationInGroup(stationId, station) &&
                UboatStationAdapter.TryGetStationAction(stationId, station, actionId, actor, out _, out state);
        }

        public static async UniTask<bool> RequestStationAction(string stationId, ShipActivator station, string actionId,
            PlayableCharacter actor)
        {
            return IsStationInGroup(stationId, station) &&
                UboatStationAdapter.TryGetStationAction(stationId, station, actionId, actor, out var template,
                    out ActionState state) && state == ActionState.Enabled &&
                (await UboatStationAdapter.StartAction(station, template, actor)).Accepted;
        }

        public static UniTask<CharacterAction> RequestTrackedStationAction(string stationId, ShipActivator station,
            string actionId, PlayableCharacter actor)
        {
            return IsStationInGroup(stationId, station) &&
                UboatStationAdapter.TryGetStationAction(stationId, station, actionId, actor, out var template,
                    out ActionState state) && state == ActionState.Enabled
                ? StartTrackedAction(station, template, actor) : UniTask.FromResult<CharacterAction>(null);
        }

        private static bool IsStationInGroup(string stationId, ShipActivator station)
        {
            return station && Array.IndexOf(GetStations(stationId), station) >= 0;
        }

        public static async UniTask<bool> RequestAction(string stationId, string actionId, PlayableCharacter actor)
        {
            if (!UboatStationAdapter.TryGetAction(stationId, actionId, actor, out var station, out var template,
                    out ActionState state) || state != ActionState.Enabled)
            {
                return false;
            }

            return (await UboatStationAdapter.StartAction(station, template, actor)).Accepted;
        }

        public static UniTask<CharacterAction> RequestTrackedAction(string stationId, string actionId,
            PlayableCharacter actor)
        {
            return UboatStationAdapter.TryGetAction(stationId, actionId, actor, out var station, out var template,
                    out ActionState state) && state == ActionState.Enabled
                ? StartTrackedAction(station, template, actor) : UniTask.FromResult<CharacterAction>(null);
        }

        public static bool TryCancelTrackedAction(PlayableCharacter actor, CharacterAction action)
        {
            if (!actor || action == null || !TrackedActions.TryGetValue(action, out var tracked) ||
                tracked != actor || !UboatStationAdapter.IsCurrentStation(action.Activator, requireActive: false) ||
                !action.CanBeStopped || action.Stage == CharacterActionStage.Completed)
            {
                return false;
            }

            var queue = actor.ActionQueue;
            if (queue != null && queue.Contains(action))
            {
                queue.Remove(action);
                return true;
            }

            if (ReferenceEquals(actor.Action, action))
            {
                actor.StopCurrentAction();
                return true;
            }

            return false;
        }

        private static async UniTask<CharacterAction> StartTrackedAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            if (!PlayerSubmarineReadiness.CanBind)
            {
                return null;
            }

            int revision = PlayerSubmarineContext.Revision;
            var action = (await UboatStationAdapter.StartAction(station, template, actor)).Tracked;
            if (action == null || revision != PlayerSubmarineContext.Revision ||
                !UboatStationAdapter.IsCurrentStation(station, requireActive: false))
            {
                return null;
            }

            TrackedActions.Add(action, actor);
            return action;
        }

        public static bool TryGetState(string stationId, out bool hasAssignedCrew, out bool switchedOn,
            out bool operating)
        {
            TryGetStation(stationId, out var station);
            return TryGetStationState(station, out hasAssignedCrew, out switchedOn, out operating);
        }

        public static bool TryGetStationState(ShipActivator station, out bool hasAssignedCrew, out bool switchedOn,
            out bool operating)
        {
            return UboatStationAdapter.TryGetState(station, out hasAssignedCrew, out switchedOn, out operating);
        }

        public static bool TryGetStationUsers(ShipActivator station, out PlayableCharacter[] users)
        {
            if (!UboatStationAdapter.IsCurrentStation(station))
            {
                users = Array.Empty<PlayableCharacter>();
                return false;
            }

            users = station.Users.ToArray();
            return true;
        }

        public static IDisposable SubscribeStateChanged(string stationId, Action callback)
        {
            return TryGetStation(stationId, out var station) ? SubscribeStationStateChanged(station, callback) : null;
        }

        public static IDisposable SubscribeStationStateChanged(ShipActivator station, Action callback)
        {
            if (callback == null || !PlayerSubmarineReadiness.CanBind || !UboatStationAdapter.IsCurrentStation(station))
            {
                return null;
            }

            return new StationStateSubscription(station, callback);
        }
    }
}
