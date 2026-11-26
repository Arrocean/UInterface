using System;
using System.Collections.Generic;
using System.Reflection;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Sandbox;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;

namespace UInterface
{
    [HarmonyPatch]
    internal static class NativePropulsionBinding
    {
        private sealed class Profile
        {
            internal readonly string SceneName;
            internal readonly string SurfacePath;
            internal readonly string SubmergedPath;

            internal Profile(string sceneName, string surfacePath, string submergedPath)
            {
                SceneName = sceneName;
                SurfacePath = surfacePath;
                SubmergedPath = submergedPath;
            }
        }

        private static readonly Dictionary<string, Profile> Profiles =
            new Dictionary<string, Profile>(StringComparer.Ordinal);

        [Inject]
        private static SandboxPlayerWolfpack wolfpack = null;

        internal static bool Register(string submarineId, string surfacePath, string submergedPath)
        {
            if (!SubmarineRegistry.IsIdentifier(submarineId) || string.IsNullOrWhiteSpace(surfacePath) ||
                string.IsNullOrWhiteSpace(submergedPath) || string.Equals(surfacePath, submergedPath, StringComparison.Ordinal))
            {
                return false;
            }

            var definition = SubmarineRegistry.Find(submarineId);
            if (definition == null || !definition.IsCustom || Profiles.ContainsKey(definition.EntityType))
            {
                return false;
            }

            string sceneName = SubmarineRegistry.GetSceneName(definition.InteriorScene);
            if (string.IsNullOrEmpty(sceneName) ||
                SubmarineRegistry.FindByInteriorScene(sceneName)?.Id != submarineId)
            {
                return false;
            }

            if (!SubmarineFramework.TryInstallPropulsionBinding())
            {
                return false;
            }

            Profiles.Add(definition.EntityType, new Profile(sceneName, surfacePath, submergedPath));
            return true;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(PlayerShip), "Awake");
            yield return AccessTools.DeclaredMethod(typeof(PlayerShip), "Start");
        }

        private static void Prefix(PlayerShip __instance, ref PlayerShipEngine ___dieselEngine,
            ref PlayerShipEngine ___electricEngine)
        {
            string sceneName = __instance.gameObject.scene.name;
            // Saves load the scene before the serializer restores the player entity type.
            string entityType = PlayerSubmarineReadiness.IsRestoring
                ? SubmarineRegistry.FindByInteriorScene(sceneName)?.EntityType
                : wolfpack?.PlayerShipSandboxEntity?.Type?.Name ?? __instance.SandboxEntity?.Type?.Name;
            if (entityType == null || !Profiles.TryGetValue(entityType, out var profile) ||
                !string.Equals(sceneName, profile.SceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var surface = FindEngine(__instance, profile.SurfacePath);
            var submerged = FindEngine(__instance, profile.SubmergedPath);
            if (!surface || !submerged || surface == submerged)
            {
                throw new InvalidOperationException("[UInterface] '" + entityType +
                    "' requires a distinct PlayerShipEngine with Interactable at each propulsion path: '" +
                    profile.SurfacePath + "' and '" + profile.SubmergedPath + "'.");
            }

            ___dieselEngine = surface;
            ___electricEngine = submerged;
        }

        private static PlayerShipEngine FindEngine(PlayerShip ship, string path)
        {
            var mount = ship.Find(path);
            if (!mount || !mount.GetComponent<Interactable>())
            {
                return null;
            }

            var engines = mount.GetComponents<PlayerShipEngine>();
            return engines.Length == 1 ? engines[0] : null;
        }
    }
}
