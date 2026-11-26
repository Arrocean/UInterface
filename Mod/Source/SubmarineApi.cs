using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        public static event Action<PlayerShip, PlayerShip> OnPlayerSubmarineChanged;
        public static event Action<PlayerShip> OnPlayerSubmarineReady;
        public static event Action<PlayerShip> OnPlayerSubmarineRestored;
        public static event Action<PlayerShip, Transform> OnPlayerSubmarineDependencyReady;

        public static int GetFeatureVersion(string featureId)
        {
            return SubmarineFramework.GetFeatureVersion(featureId);
        }
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
            ship = PlayerSubmarineContext.Ship;
            submarineId = ship && PlayerSubmarineReadiness.CanBind ? PlayerSubmarineContext.SubmarineId : null;
            return submarineId != null;
        }

        public static bool TryGetReadySubmarine(out string submarineId, out PlayerShip ship)
        {
            if (PlayerSubmarineReadiness.IsReady && TryGetCurrentSubmarine(out submarineId, out ship))
            {
                return true;
            }

            submarineId = null;
            ship = null;
            return false;
        }

        public static bool TryGetMount(string mountId, out Transform mount)
        {
            mount = null;
            if (!TryGetCurrentSubmarine(out var submarineId, out var ship) || !SubmarineRegistry.IsIdentifier(mountId))
            {
                return false;
            }

            string transformPath = GetMapping(Mounts, MountSheet, submarineId + ":" + mountId, "Transform");
            if (!string.IsNullOrWhiteSpace(transformPath))
            {
                mount = ShipMountResolver.Find(ship, transformPath);
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
            var previousShip = PlayerSubmarineContext.Ship;
            if (ReferenceEquals(previousShip, nextShip))
            {
                return;
            }

            PlayerSubmarineContext.SetShip(nextShip, entityType);
            int revision = PlayerSubmarineContext.Invalidate();
            if (revision != PlayerSubmarineContext.Revision)
            {
                return;
            }

            PlayerSubmarineReadiness.ObserveShip(nextShip);

            var handlers = OnPlayerSubmarineChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<PlayerShip, PlayerShip> handler in handlers.GetInvocationList())
            {
                if (revision != PlayerSubmarineContext.Revision)
                {
                    break;
                }

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

        internal static void NotifyReady(PlayerShip ship, bool restored, int revision)
        {
            NotifyShip(OnPlayerSubmarineReady, ship, revision);
            if (restored)
            {
                NotifyShip(OnPlayerSubmarineRestored, ship, revision);
            }
        }

        internal static async UniTask NotifyDependencyReady(PlayerShip ship, Transform root, int revision)
        {
            await UniTask.NextFrame();
            var handlers = OnPlayerSubmarineDependencyReady;
            if (handlers == null || !TryGetReadySubmarine(out _, out var current) || current != ship)
            {
                return;
            }

            foreach (Action<PlayerShip, Transform> handler in handlers.GetInvocationList())
            {
                if (!root || !root.gameObject.scene.isLoaded || PlayerSubmarineContext.Revision != revision)
                {
                    break;
                }

                try
                {
                    handler(ship, root);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void NotifyShip(Action<PlayerShip> handlers, PlayerShip ship, int revision)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action<PlayerShip> handler in handlers.GetInvocationList())
            {
                if (!ship || PlayerSubmarineContext.Revision != revision)
                {
                    break;
                }

                try
                {
                    handler(ship);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (PlayerSubmarineContext.SceneHandle == scene.handle)
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

    [HarmonyPatch(typeof(Entity), nameof(Entity.OnDependencySceneLoaded))]
    internal static class PlayerSubmarineDependencyLifecycle
    {
        private static void Postfix(Entity __instance, GameObject rootGameObject)
        {
            if (__instance is PlayerShip ship && rootGameObject &&
                SubmarineApi.TryGetCurrentSubmarine(out _, out var current) && current == ship)
            {
                SubmarineApi.NotifyDependencyReady(ship, rootGameObject.transform, PlayerSubmarineContext.Revision).Forget();
            }
        }
    }
}
