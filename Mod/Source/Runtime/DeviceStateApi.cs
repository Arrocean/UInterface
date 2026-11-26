using System;
using System.Collections.Generic;
using System.Text;

namespace UInterface
{
    public static class DeviceStateApi
    {
        public const int ApiVersion = 1;
        internal const string FeatureId = "uinterface.device-state";
        internal const int MaximumEntries = 256;
        internal const int MaximumIdBytes = 256;
        internal const int MaximumDataBytes = 8192;

        private static Dictionary<string, State> states = new Dictionary<string, State>(StringComparer.Ordinal);
        private static Dictionary<string, State> restoring;

        static DeviceStateApi()
        {
            PlayerSubmarineContext.Invalidated += () => states.Clear();
        }

        public static bool TryRead(string deviceId, out int version, out string data)
        {
            version = 0;
            data = null;
            if (!CanAccess() || !IsValidId(deviceId) || !states.TryGetValue(deviceId, out var state))
            {
                return false;
            }

            version = state.Version;
            data = state.Data;
            return true;
        }

        public static bool TryWrite(string deviceId, int version, string data)
        {
            if (!CanAccess() || !IsValidId(deviceId) || version < 1 || data == null ||
                Encoding.UTF8.GetByteCount(data) > MaximumDataBytes ||
                (states.Count >= MaximumEntries && !states.ContainsKey(deviceId)))
            {
                return false;
            }

            states[deviceId] = new State(version, data);
            return true;
        }

        public static bool TryRemove(string deviceId)
        {
            return CanAccess() && IsValidId(deviceId) && states.Remove(deviceId);
        }

        private static bool CanAccess()
        {
            return SubmarineApi.GetFeatureVersion(FeatureId) > 0 && SubmarineApi.TryGetReadySubmarine(out _, out _);
        }

        internal static bool IsValidId(string id)
        {
            return SubmarineRegistry.IsIdentifier(id) && Encoding.UTF8.GetByteCount(id) <= MaximumIdBytes;
        }

        internal static IReadOnlyDictionary<string, State> GetStatesForSave()
        {
            return CanAccess() ? states : null;
        }

        internal static void BeginRestore()
        {
            restoring = null;
        }

        internal static void StageRestore(Dictionary<string, State> snapshot)
        {
            if (PlayerSubmarineReadiness.IsRestoring)
            {
                restoring = snapshot;
            }
        }

        internal static void FinishRestore(bool success)
        {
            if (success && restoring != null)
            {
                states = restoring;
            }

            restoring = null;
        }

        internal sealed class State
        {
            internal readonly int Version;
            internal readonly string Data;

            internal State(int version, string data)
            {
                Version = version;
                Data = data;
            }
        }
    }
}
