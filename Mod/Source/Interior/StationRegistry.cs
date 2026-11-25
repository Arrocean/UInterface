using System;
using System.Collections.Generic;
using UBOAT.Game.Scene.Characters.Actions;

namespace UInterface
{
    internal static class StationRegistry
    {
        private const string StationSheet = "Sandbox/UInterfaceStations/";

        private static readonly Dictionary<string, string> Mounts =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> StockTags =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Dictionary<string, Type>> Actions =
            new Dictionary<string, Dictionary<string, Type>>(StringComparer.Ordinal);

        internal static bool RegisterStation(string submarineId, string stationId, string mountId)
        {
            if (!SubmarineRegistry.IsIdentifier(submarineId) || !SubmarineRegistry.IsIdentifier(stationId) ||
                !SubmarineRegistry.IsIdentifier(mountId))
            {
                return false;
            }

            string key = submarineId + ":" + stationId;
            if (Mounts.ContainsKey(key) || SubmarineRegistry.ReadRow(StationSheet + key) != null)
            {
                return false;
            }

            Mounts.Add(key, mountId);
            return true;
        }

        internal static bool RegisterAction(string stationId, string actionId, Type actionType)
        {
            if (!SubmarineRegistry.IsIdentifier(stationId) || !SubmarineRegistry.IsIdentifier(actionId) ||
                actionType == null || actionType.IsAbstract || !typeof(CharacterAction).IsAssignableFrom(actionType))
            {
                return false;
            }

            if (!Actions.TryGetValue(stationId, out var actions))
            {
                actions = new Dictionary<string, Type>(StringComparer.Ordinal);
                Actions.Add(stationId, actions);
            }

            if (actions.ContainsKey(actionId))
            {
                return false;
            }

            actions.Add(actionId, actionType);
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

        internal static bool TryGetMountId(string submarineId, string stationId, out string mountId)
        {
            string key = submarineId + ":" + stationId;
            var row = SubmarineRegistry.ReadRow(StationSheet + key);
            if (row != null)
            {
                mountId = row.GetString("Mount");
                return true;
            }

            return Mounts.TryGetValue(key, out mountId);
        }

        internal static bool TryGetStockTag(string submarineId, string stationId, out string tag)
        {
            tag = null;
            return SubmarineRegistry.VanillaTypes.Contains(submarineId) && StockTags.TryGetValue(stationId, out tag);
        }

        internal static IReadOnlyDictionary<string, Type> GetActions(string stationId)
        {
            Actions.TryGetValue(stationId, out var actions);
            return actions;
        }
    }
}
