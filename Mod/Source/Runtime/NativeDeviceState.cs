using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UBOAT.Game.Serialization;
using UnityEngine;

namespace UInterface
{
    [HarmonyPatch]
    internal static class DeviceStateWrite
    {
        internal static readonly byte[] Marker = Encoding.ASCII.GetBytes("UISTATE1");

        private static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(GameStateSerializer)).Single(method =>
                method.Name == nameof(GameStateSerializer.Serialize) && method.GetParameters().Length == 2 &&
                method.GetParameters()[0].ParameterType.FullName == "System.IO.BinaryWriter" &&
                method.GetParameters()[1].ParameterType == typeof(object));
        }

        private static void Postfix(GameStateSerializer __instance, object __1)
        {
            if (!(__1 is SaveData) || __instance.NestingLevel != 0)
            {
                return;
            }

            var states = DeviceStateApi.GetStatesForSave();
            if (states == null || states.Count == 0)
            {
                return;
            }

            __instance.Write(Marker);
            __instance.Write(states.Count);
            foreach (var entry in states)
            {
                WriteText(__instance, entry.Key);
                __instance.Write(entry.Value.Version);
                WriteText(__instance, entry.Value.Data);
            }
        }

        private static void WriteText(GameStateSerializer serializer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            serializer.Write(bytes.Length);
            serializer.Write(bytes);
        }
    }

    [HarmonyPatch]
    internal static class DeviceStateRead
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(GameStateDeserializer)).Single(method =>
                method.Name == nameof(GameStateDeserializer.Deserialize) && method.GetParameters().Length == 2 &&
                method.GetParameters()[0].ParameterType == typeof(int));
        }

        private static void Postfix(GameStateDeserializer __instance, int __0, object __result)
        {
            if (__0 != -1 || !(__result is SaveData))
            {
                return;
            }

            try
            {
                if (!__instance.ReadBytes(DeviceStateWrite.Marker.Length).SequenceEqual(DeviceStateWrite.Marker))
                {
                    return;
                }

                int count = __instance.ReadInt32();
                if (count < 0 || count > DeviceStateApi.MaximumEntries)
                {
                    throw new FormatException("Invalid UInterface device state count.");
                }

                var states = new Dictionary<string, DeviceStateApi.State>(count, StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    string id = ReadText(__instance, DeviceStateApi.MaximumIdBytes);
                    int version = __instance.ReadInt32();
                    string data = ReadText(__instance, DeviceStateApi.MaximumDataBytes);
                    if (!DeviceStateApi.IsValidId(id) || version < 1 || states.ContainsKey(id))
                    {
                        throw new FormatException("Invalid UInterface device state entry.");
                    }

                    states.Add(id, new DeviceStateApi.State(version, data));
                }

                __instance.MainThreadBlock(() => DeviceStateApi.StageRestore(states), exitOnException: false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[UInterface] Could not read device state from the save: " + exception.Message);
            }
        }

        private static string ReadText(GameStateDeserializer deserializer, int maximum)
        {
            int size = deserializer.ReadInt32();
            if (size < 0 || size > maximum)
            {
                throw new FormatException("Invalid UInterface device state length.");
            }

            byte[] bytes = deserializer.ReadBytes(size);
            if (bytes.Length != size)
            {
                throw new FormatException("Truncated UInterface device state.");
            }

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
