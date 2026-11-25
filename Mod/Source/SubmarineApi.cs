using System;
using System.Collections.Generic;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UInterface
{
    public static class SubmarineApi
    {
        public const int ApiVersion = 1;

        private const string MountSheet = "Sandbox/UInterfaceMounts/";
        private const string EquipmentSheet = "Sandbox/UInterfaceEquipment/";

        private static readonly Dictionary<string, string> Mounts =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> EquipmentMountIds =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static PlayerShip currentShip;
        private static int? currentSceneHandle;
        private static string currentSubmarineId;

        public static event Action<PlayerShip, PlayerShip> OnPlayerSubmarineChanged;

        public static bool RegisterSubmarine(string id, string baseType, string entityType, string interiorScene)
        {
            return SubmarineRegistry.Register(id, baseType, entityType, interiorScene);
        }

        public static bool TryGetSubmarine(string id, out string baseType, out string entityType, out string interiorScene)
        {
            var definition = SubmarineRegistry.Find(id);
            baseType = definition?.BaseType;
            entityType = definition?.EntityType;
            interiorScene = definition?.InteriorScene;
            return definition != null;
        }

        public static bool RegisterMount(string submarineId, string mountId, string transformPath)
        {
            return SubmarineRegistry.IsIdentifier(submarineId) && SubmarineRegistry.IsIdentifier(mountId) &&
                RegisterMapping(Mounts, MountSheet, submarineId + ":" + mountId, transformPath);
        }

        public static bool RegisterEquipment(string equipmentId, string mountId)
        {
            return SubmarineRegistry.IsIdentifier(equipmentId) && SubmarineRegistry.IsIdentifier(mountId) &&
                RegisterMapping(EquipmentMountIds, EquipmentSheet, equipmentId, mountId);
        }

        public static bool TryGetCurrentSubmarine(out string submarineId, out PlayerShip ship)
        {
            ship = currentShip;
            submarineId = ship ? currentSubmarineId : null;
            return submarineId != null;
        }

        public static bool TryGetMount(string mountId, out Transform mount)
        {
            mount = null;
            if (!currentShip || currentSubmarineId == null || !SubmarineRegistry.IsIdentifier(mountId))
            {
                return false;
            }

            string transformPath = GetMapping(Mounts, MountSheet, currentSubmarineId + ":" + mountId, "Transform");
            if (!string.IsNullOrWhiteSpace(transformPath))
            {
                mount = currentShip.Find(transformPath, lookInDependencyScenes: true);
            }

            return mount != null;
        }

        public static bool TryGetEquipmentMount(string equipmentId, out Transform mount)
        {
            mount = null;
            return SubmarineRegistry.IsIdentifier(equipmentId) &&
                TryGetMount(GetMapping(EquipmentMountIds, EquipmentSheet, equipmentId, "Mount"), out mount);
        }

        internal static void TrackSceneUnloads()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        internal static void UpdatePlayerSubmarine(PlayerShip nextShip, string entityType)
        {
            if (ReferenceEquals(currentShip, nextShip))
            {
                return;
            }

            var previousShip = currentShip;
            string nextId = nextShip ? SubmarineRegistry.GetLoadedId(entityType) : null;
            currentShip = nextShip;
            currentSubmarineId = nextId;
            currentSceneHandle = nextShip ? (int?)nextShip.gameObject.scene.handle : null;

            var handlers = OnPlayerSubmarineChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<PlayerShip, PlayerShip> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(previousShip, nextShip);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (currentSceneHandle == scene.handle)
            {
                UpdatePlayerSubmarine(null, null);
            }
        }

        private static bool RegisterMapping(Dictionary<string, string> mappings, string sheet, string id, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || mappings.ContainsKey(id) ||
                SubmarineRegistry.ReadRow(sheet + id) != null)
            {
                return false;
            }

            mappings.Add(id, value);
            return true;
        }

        private static string GetMapping(Dictionary<string, string> mappings, string sheet, string id, string column)
        {
            var row = SubmarineRegistry.ReadRow(sheet + id);
            if (row != null)
            {
                return row.GetString(column);
            }

            mappings.TryGetValue(id, out string value);
            return value;
        }
    }

    [HarmonyPatch(typeof(PlayerShipProxy), "RaiseChangeEvent")]
    internal static class PlayerSubmarineLifecycle
    {
        private static void Postfix(PlayerShipProxy __instance, PlayerShipChangeEvent e)
        {
            SubmarineApi.UpdatePlayerSubmarine(e.NewPlayerShip,
                e.NewPlayerShip ? __instance.SandboxEntity?.Type?.Name : null);
        }
    }
}
