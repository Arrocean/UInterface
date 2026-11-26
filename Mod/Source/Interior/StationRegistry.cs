using System;
using System.Collections.Generic;
using UBOAT.Game.Scene.Characters.Actions;

namespace UInterface
{
    internal static class StationRegistry
    {
        internal sealed class ActionBinding
        {
            internal readonly Type Type;
            internal readonly Func<CharacterAction, bool> Selector;

            internal ActionBinding(Type type, Func<CharacterAction, bool> selector)
            {
                Type = type;
                Selector = selector;
            }
        }

        private const string StationSheet = "Sandbox/UInterfaceStations/";

        private static readonly Dictionary<string, string[]> Mounts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> StockTags =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Dictionary<string, ActionBinding>> Actions =
            new Dictionary<string, Dictionary<string, ActionBinding>>(StringComparer.Ordinal);

        internal static bool RegisterStation(string submarineId, string stationId, string mountId)
        {
            return RegisterStationGroup(submarineId, stationId, new[] { mountId });
        }

        internal static bool RegisterStationGroup(string submarineId, string stationId, string[] mountIds)
        {
            if (!SubmarineRegistry.IsIdentifier(submarineId) || !SubmarineRegistry.IsIdentifier(stationId) ||
                !AreMountIdsValid(mountIds))
            {
                return false;
            }

            string key = submarineId + ":" + stationId;
            if (Mounts.ContainsKey(key) || SubmarineRegistry.ReadRow(StationSheet + key) != null)
            {
                return false;
            }

            Mounts.Add(key, (string[])mountIds.Clone());
            return true;
        }

        internal static bool RegisterAction(string stationId, string actionId, Type actionType,
            Func<CharacterAction, bool> selector = null)
        {
            return SubmarineRegistry.IsIdentifier(stationId) &&
                RegisterActionForKey(stationId, actionId, actionType, selector);
        }

        internal static bool RegisterSubmarineAction(string submarineId, string stationId, string actionId,
            Type actionType, Func<CharacterAction, bool> selector)
        {
            return SubmarineRegistry.IsIdentifier(submarineId) && SubmarineRegistry.IsIdentifier(stationId) &&
                RegisterActionForKey(submarineId + ":" + stationId, actionId, actionType, selector);
        }

        private static bool RegisterActionForKey(string key, string actionId, Type actionType,
            Func<CharacterAction, bool> selector)
        {
            if (!SubmarineRegistry.IsIdentifier(actionId) ||
                actionType == null || actionType.IsAbstract || !typeof(CharacterAction).IsAssignableFrom(actionType))
            {
                return false;
            }

            if (!Actions.TryGetValue(key, out var actions))
            {
                actions = new Dictionary<string, ActionBinding>(StringComparer.Ordinal);
                Actions.Add(key, actions);
            }

            if (actions.ContainsKey(actionId))
            {
                return false;
            }

            actions.Add(actionId, new ActionBinding(actionType, selector));
            return true;
        }

        internal static void AddStockStation(string stationId, string tag, string actionId, Type actionType)
        {
            AddStockStation(stationId, tag);
            RegisterAction(stationId, actionId, actionType);
        }

        internal static void AddStockStation(string stationId, string tag)
        {
            StockTags.Add(stationId, tag);
        }

        internal static bool TryGetMountIds(string submarineId, string stationId, out string[] mountIds)
        {
            string key = submarineId + ":" + stationId;
            var row = SubmarineRegistry.ReadRow(StationSheet + key);
            if (row != null)
            {
                string group = row.GetString("Mounts");
                var ids = string.IsNullOrWhiteSpace(group)
                    ? new[] { row.GetString("Mount") } : group.Split(',');
                for (int index = 0; index < ids.Length; index++)
                {
                    ids[index] = ids[index]?.Trim();
                }

                mountIds = AreMountIdsValid(ids) ? ids : Array.Empty<string>();
                return true;
            }

            return Mounts.TryGetValue(key, out mountIds);
        }

        private static bool AreMountIdsValid(string[] mountIds)
        {
            if (mountIds == null || mountIds.Length == 0)
            {
                return false;
            }

            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (string mountId in mountIds)
            {
                if (!SubmarineRegistry.IsIdentifier(mountId) || !distinct.Add(mountId))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool TryGetStockTag(string submarineId, string stationId, out string tag)
        {
            tag = null;
            return SubmarineRegistry.VanillaTypes.Contains(submarineId) && StockTags.TryGetValue(stationId, out tag);
        }

        internal static IReadOnlyDictionary<string, ActionBinding> GetActions(string stationId)
        {
            Actions.TryGetValue(stationId, out var defaults);
            var submarineId = PlayerSubmarineContext.SubmarineId;
            if (submarineId == null || !Actions.TryGetValue(submarineId + ":" + stationId, out var local))
            {
                return defaults;
            }

            if (defaults == null)
            {
                return local;
            }

            var applicable = new Dictionary<string, ActionBinding>(defaults, StringComparer.Ordinal);
            foreach (var entry in local)
            {
                applicable[entry.Key] = entry.Value;
            }

            return applicable;
        }
    }
}
