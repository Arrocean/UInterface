using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
        internal static bool TryGetStation(string stationId, out ShipActivator station)
        {
            var stations = GetStations(stationId);
            station = stations.Length == 1 ? stations[0] : null;
            return station != null;
        }

        internal static ShipActivator[] GetStations(string stationId)
        {
            if (!SubmarineRegistry.IsIdentifier(stationId) ||
                !SubmarineApi.TryGetCurrentSubmarine(out string submarineId, out var ship) || !ship.Interior)
            {
                return Array.Empty<ShipActivator>();
            }

            Transform mount = null;
            string tag = null;
            if (StationRegistry.TryGetMountId(submarineId, stationId, out string mountId))
            {
                if (!SubmarineApi.TryGetMount(mountId, out mount))
                {
                    return Array.Empty<ShipActivator>();
                }
            }
            else if (!StationRegistry.TryGetStockTag(submarineId, stationId, out tag))
            {
                return Array.Empty<ShipActivator>();
            }

            var stations = new List<ShipActivator>();
            foreach (var candidate in ship.Interior.GetComponentsInChildren<ShipActivator>())
            {
                if (!candidate || !candidate.isActiveAndEnabled || candidate.ParentEntity != ship)
                {
                    continue;
                }

                bool matches = mount
                    ? candidate.transform == mount ||
                        (candidate.AssociatedEquipment && candidate.AssociatedEquipment.transform == mount)
                    : candidate.HasTag(tag);
                if (matches)
                {
                    stations.Add(candidate);
                }
            }

            return stations.ToArray();
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

        internal static UniTask<bool> RequestNativeAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            if (template != null && actor && IsCurrentStation(station))
            {
                var nativeActions = station.GetActionInfos(actor, ActionContext.Normal, isPlayerOrder: true);
                foreach (var info in nativeActions)
                {
                    if (ReferenceEquals(info.ActionTemplate, template) && info.State == ActionState.Enabled)
                    {
                        return StartAction(station, template, actor);
                    }
                }
            }

            return UniTask.FromResult(false);
        }

        private static bool IsCurrentStation(ShipActivator station)
        {
            return station && station.isActiveAndEnabled &&
                SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) && station.ParentEntity == ship;
        }

        internal static string[] QueryActions(string stationId, ShipActivator station, PlayableCharacter actor)
        {
            var bindings = StationRegistry.GetActions(stationId);
            if (bindings == null)
            {
                return Array.Empty<string>();
            }

            var infos = station.GetActionInfos(actor, ActionContext.Normal, isPlayerOrder: true);
            var actions = new List<string>();
            foreach (var binding in bindings)
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

            var bindings = StationRegistry.GetActions(stationId);
            if (bindings == null || !bindings.TryGetValue(actionId, out Type actionType))
            {
                return false;
            }

            var infos = station.GetActionInfos(actor, ActionContext.Normal, isPlayerOrder: true);
            if (!TryMatchAction(infos, actionType, out var info))
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
            if (!station || !(station.AssociatedEquipment is Device device))
            {
                return false;
            }

            hasAssignedCrew = station.Users.Count > 0;
            switchedOn = !device.HasDisableFlag(DeviceDisableFlag.TurnedOff);
            operating = device.isActiveAndEnabled;
            return true;
        }

        private static bool TryMatchAction(List<CharacterActionInfo> infos, Type actionType,
            out CharacterActionInfo match)
        {
            match = default(CharacterActionInfo);
            foreach (var info in infos)
            {
                if (info.ActionTemplate != null && info.ActionTemplate.GetType() == actionType)
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

        internal static async UniTask<bool> StartAction(ShipActivator station, CharacterAction template,
            PlayableCharacter actor)
        {
            var action = template.Duplicate(station);
            action.IsDirectPlayerOrder = true;
            await actor.StartAction(station, action);
            return action.Stage == CharacterActionStage.Completed || action.Characters.Contains(actor);
        }
    }
}
