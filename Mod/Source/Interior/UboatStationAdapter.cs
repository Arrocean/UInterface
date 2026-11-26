using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using UBOAT.Game.Scene.Characters.Skills;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using ShipActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace UInterface
{
    internal static class UboatStationAdapter
    {
        [Inject]
        private static PlayerCrew crew = null;

        internal static bool TryGetStation(string stationId, out ShipActivator station)
        {
            var stations = GetStations(stationId);
            station = stations.Length == 1 ? stations[0] : null;
            return station != null;
        }

        internal static ShipActivator[] GetStations(string stationId)
        {
            if (!SubmarineRegistry.IsIdentifier(stationId) ||
                !SubmarineApi.TryGetCurrentSubmarine(out string submarineId, out var ship))
            {
                return Array.Empty<ShipActivator>();
            }

            HashSet<Transform> mounts = null;
            string tag = null;
            if (StationRegistry.TryGetMountIds(submarineId, stationId, out var mountIds))
            {
                mounts = new HashSet<Transform>();
                foreach (string mountId in mountIds)
                {
                    if (SubmarineApi.TryGetMount(mountId, out var mount))
                    {
                        mounts.Add(mount);
                    }
                }

                if (mounts.Count == 0)
                {
                    return Array.Empty<ShipActivator>();
                }
            }
            else if (!StationRegistry.TryGetStockTag(submarineId, stationId, out tag))
            {
                return Array.Empty<ShipActivator>();
            }

            var stations = new List<ShipActivator>();
            if (ship.Interior)
            {
                CollectStations(ship.Interior.transform);
            }

            if (mounts != null)
            {
                CollectStations(ship.transform);
            }

            foreach (var root in ShipMountResolver.GetDependencyRoots(ship))
            {
                CollectStations(root);
            }

            return stations.ToArray();

            void CollectStations(Transform root)
            {
                foreach (var candidate in root.GetComponentsInChildren<ShipActivator>())
                {
                    if (!candidate || !candidate.isActiveAndEnabled || candidate.ParentEntity != ship)
                    {
                        continue;
                    }

                    bool matches = mounts != null
                        ? mounts.Contains(candidate.transform) ||
                            (candidate.AssociatedEquipment && mounts.Contains(candidate.AssociatedEquipment.transform))
                        : candidate.HasTag(tag);
                    if (matches && !stations.Contains(candidate))
                    {
                        stations.Add(candidate);
                    }
                }
            }
        }

        internal static CharacterActionInfo[] QueryNativeActions(ShipActivator station, PlayableCharacter actor)
        {
            if (!actor || !IsCurrentStation(station))
            {
                return Array.Empty<CharacterActionInfo>();
            }

            var nativeActions = station.GetActionInfos(actor, ActionContext.Normal, isPlayerOrder: true);
            var visible = new List<CharacterActionInfo>();
            foreach (var info in nativeActions)
            {
                if (info.ActionTemplate != null && info.State != ActionState.Hidden)
                {
                    visible.Add(info);
                }
            }

            return visible.ToArray();
        }

        internal static CharacterActionInfo[] QueryQueuedNativeActions(ShipActivator station, PlayableCharacter actor)
        {
            if (!IsCurrentStation(station) || GetActionQueue(actor) == null)
            {
                return Array.Empty<CharacterActionInfo>();
            }

            var actions = new List<CharacterActionInfo>();
            var context = actor ? ActionContext.Normal : ActionContext.Queue;
            foreach (var info in station.GetActionInfos(actor, context, isPlayerOrder: true))
            {
                if (info.ActionTemplate != null &&
                    (info.State == ActionState.Enabled || info.State == ActionState.Disabled) &&
                    !VanillaActionRules.OpensStorageWithoutActor(info.ActionTemplate) &&
                    !VanillaActionRules.CompletesBeforeQueue(station, info.ActionTemplate.GetType()))
                {
                    actions.Add(info);
                }
            }

            return actions.ToArray();
        }

        internal static ActionQueue GetActionQueue(PlayableCharacter actor)
        {
            if (crew == null)
            {
                return null;
            }

            if (ReferenceEquals(actor, null))
            {
                return crew.GlobalActionQueue;
            }

            return actor && crew.Contains(actor) ? actor.ActionQueue : null;
        }

        internal static async UniTask<CharacterAction> QueueNativeAction(ShipActivator station,
            CharacterAction template, PlayableCharacter actor, ActionQueue queue)
        {
            if (queue == null || template == null || !PlayerSubmarineReadiness.CanBind ||
                Array.FindIndex(QueryQueuedNativeActions(station, actor),
                    info => ReferenceEquals(info.ActionTemplate, template) && info.State == ActionState.Enabled) < 0 ||
                (actor && queue.HasAction(template, station)))
            {
                return null;
            }

            int revision = PlayerSubmarineContext.Revision;
            var action = template.Duplicate(station);
            action.IsDirectPlayerOrder = true;
            if (!actor)
            {
                action.Silent = true;
            }

            // The original queue's Add invokes pre-start without a selected crew member.
            if (!await action.OnPreStart(null) || revision != PlayerSubmarineContext.Revision ||
                !IsCurrentStation(station) || GetActionQueue(actor) != queue ||
                (actor && queue.HasAction(template, station)))
            {
                return null;
            }

            queue.AddPreStarted(action);
            return queue.Contains(action) || action.Stage == CharacterActionStage.Completed ? action : null;
        }

        internal static CharacterActionInfo[] QueryNativeUtilityActions(ShipActivator station, PlayableCharacter actor)
        {
            if (!IsCurrentStation(station))
            {
                return Array.Empty<CharacterActionInfo>();
            }

            var context = actor ? ActionContext.Normal : ActionContext.NoCharacter;
            var utility = new List<CharacterActionInfo>();
            foreach (var info in station.GetActionInfos(actor, context, isPlayerOrder: true))
            {
                if (IsUtility(info, actor))
                {
                    utility.Add(info);
                }
            }

            return utility.ToArray();
        }

        internal static bool CanRequestNativeAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            if (template != null && actor && IsCurrentStation(station))
            {
                var nativeActions = station.GetActionInfos(actor, ActionContext.Normal, isPlayerOrder: true);
                foreach (var info in nativeActions)
                {
                    if (ReferenceEquals(info.ActionTemplate, template) && info.State == ActionState.Enabled)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static async UniTask<bool> RequestNativeUtilityAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            if (template == null || !PlayerSubmarineReadiness.CanBind || !IsCurrentStation(station) ||
                (actor ? !actor.CanStartNormalActions() : crew == null))
            {
                return false;
            }

            var context = actor ? ActionContext.Normal : ActionContext.NoCharacter;
            foreach (var info in station.GetActionInfos(actor, context, isPlayerOrder: true))
            {
                if (!ReferenceEquals(info.ActionTemplate, template) || !IsUtility(info, actor))
                {
                    continue;
                }

                var action = template.Duplicate(station);
                action.IsDirectPlayerOrder = true;
                if (actor)
                {
                    await actor.StartAction(station, action);
                }
                else
                {
                    action.Silent = true;
                    await crew.GlobalActionQueue.Add(action);
                }

                return true;
            }

            return false;
        }

        private static bool IsUtility(CharacterActionInfo info, PlayableCharacter actor)
        {
            return info.ActionTemplate != null && (info.State == ActionState.Utility ||
                // With no selected actor the stock storage actions open their UI during pre-start.
                (!actor && info.State == ActionState.Enabled && VanillaActionRules.OpensStorageWithoutActor(info.ActionTemplate)));
        }

        internal static bool IsCurrentStation(ShipActivator station, bool requireActive = true)
        {
            return station && (!requireActive || station.isActiveAndEnabled) &&
                SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) && station.ParentEntity == ship;
        }

        internal static string[] QueryActions(string stationId, ShipActivator station, PlayableCharacter actor)
        {
            var bindings = StationRegistry.GetActions(stationId);
            if (bindings == null)
            {
                return Array.Empty<string>();
            }

            var infos = new List<CharacterActionInfo>();
            station.GetActionInfos(infos, actor, ActionContext.Normal, isPlayerOrder: true);
            var actions = new List<string>();
            foreach (var binding in bindings.ToArray())
            {
                if (TryMatchAction(infos, binding.Value, out var info) && info.State != ActionState.Hidden)
                {
                    actions.Add(binding.Key);
                }
            }

            return actions.ToArray();
        }

        internal static bool TryGetAction(string stationId, string actionId, PlayableCharacter actor,
            out ShipActivator station, out CharacterAction action, out ActionState state)
        {
            station = null;
            action = null;
            state = ActionState.Hidden;
            if (!actor || !SubmarineRegistry.IsIdentifier(actionId) || !TryGetStation(stationId, out station))
            {
                return false;
            }

            return TryGetStationAction(stationId, station, actionId, actor, out action, out state);
        }

        internal static bool TryGetStationAction(string stationId, ShipActivator station, string actionId,
            PlayableCharacter actor, out CharacterAction action, out ActionState state)
        {
            action = null;
            state = ActionState.Hidden;
            if (!actor || !SubmarineRegistry.IsIdentifier(stationId) || !SubmarineRegistry.IsIdentifier(actionId) ||
                !IsCurrentStation(station))
            {
                return false;
            }

            var bindings = StationRegistry.GetActions(stationId);
            if (bindings == null || !bindings.TryGetValue(actionId, out var binding))
            {
                return false;
            }

            var infos = new List<CharacterActionInfo>();
            station.GetActionInfos(infos, actor, ActionContext.Normal, isPlayerOrder: true);
            if (!TryMatchAction(infos, binding, out var info))
            {
                return false;
            }

            action = info.ActionTemplate;
            state = info.State;
            return true;
        }

        internal static bool TryGetState(ShipActivator station, out bool hasAssignedCrew, out bool switchedOn,
            out bool operating)
        {
            hasAssignedCrew = false;
            switchedOn = false;
            operating = false;
            if (!IsCurrentStation(station) || !(station.AssociatedEquipment is Device device))
            {
                return false;
            }

            hasAssignedCrew = station.Users.Count > 0;
            switchedOn = !device.HasDisableFlag(DeviceDisableFlag.TurnedOff);
            operating = device.isActiveAndEnabled;
            return true;
        }

        private static bool TryMatchAction(List<CharacterActionInfo> infos, StationRegistry.ActionBinding binding,
            out CharacterActionInfo match)
        {
            match = default(CharacterActionInfo);
            foreach (var info in infos)
            {
                if (info.ActionTemplate != null && info.ActionTemplate.GetType() == binding.Type &&
                    (binding.Selector == null || binding.Selector(info.ActionTemplate)))
                {
                    if (match.ActionTemplate != null && match.ActionTemplate != info.ActionTemplate)
                    {
                        return false;
                    }

                    match = info;
                }
            }

            return match.ActionTemplate != null;
        }

        internal static async UniTask<(bool Accepted, CharacterAction Tracked)> StartAction(
            ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            if (!PlayerSubmarineReadiness.CanBind)
            {
                return (false, null);
            }

            bool immediate = VanillaActionRules.CompletesBeforeQueue(station, template.GetType());
            if (immediate && actor.ActionQueue == null && actor.Action != null && !actor.Action.CanBeStopped)
            {
                return (false, null);
            }

            var action = template.Duplicate(station);
            action.IsDirectPlayerOrder = true;
            await actor.StartAction(station, action);
            if (action.Stage == CharacterActionStage.Completed || action.Characters.Contains(actor) ||
                actor.ActionQueue?.Contains(action) == true)
            {
                return (true, action);
            }

            // These stock operations complete in OnPreStart without creating a crew task.
            return (immediate, null);
        }

    }
}
