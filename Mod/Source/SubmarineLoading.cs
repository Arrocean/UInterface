using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DWS.Common.InjectionFramework;
using DWS.Common.Resources;
using HarmonyLib;
using UBOAT.Game.Sandbox;
using UBOAT.Game.Sandbox.Missions;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.UI;
using UnityEngine;

namespace UInterface
{
    internal static class SubmarineLoading
    {
        [Inject]
        private static EntityTypesDatabase entityTypes = null;

        [Inject]
        private static ResourceManager resources = null;

        internal static bool HasAssets(SubmarineDefinition definition)
        {
            return SubmarineRegistry.FindByEntityType(definition.EntityType)?.Id == definition.Id &&
                entityTypes.HasEntityType(definition.EntityType) &&
                (resources.GetAssetMod(definition.InteriorScene) != null ||
                    Application.CanStreamedLevelBeLoaded(definition.InteriorScene));
        }

        internal static void ValidateAssets(SubmarineDefinition definition)
        {
            if (!HasAssets(definition))
            {
                throw new InvalidOperationException("[UInterface] Cannot load '" + definition.Id +
                    "'. Check Entity Type '" + definition.EntityType + "' and Interior Scene '" +
                    definition.InteriorScene + "' in the content pack.");
            }
        }
    }

    [HarmonyPatch(typeof(StartScript), "LoadPlayerShip", typeof(string), typeof(string))]
    internal static class CampaignSubmarineLoader
    {
        [Inject]
        private static IPlayerShipProxy playerShip = null;

        [Inject]
        private static PlayerCareer career = null;

        private static bool Prefix(ref string shipType, string shipName, ref UniTask __result)
        {
            var definition = SubmarineRegistry.Find(shipType);
            if (definition == null)
            {
                return true;
            }

            if (!definition.IsCustom)
            {
                Debug.Log("[UInterface] Loading campaign submarine '" + shipType + "' as '" + definition.BaseType + "'.");
                shipType = definition.BaseType;
                return true;
            }

            SubmarineLoading.ValidateAssets(definition);
            __result = LoadCustomSubmarine(definition, shipName);
            return false;
        }

        private static async UniTask LoadCustomSubmarine(SubmarineDefinition definition, string shipName)
        {
            await playerShip.LoadShipType(definition.InteriorScene, definition.EntityType, shipName);
            career.UnlockShip(definition.PortType, allowFreeUpgrade: false);
        }
    }

    [HarmonyPatch(typeof(PlayerShipProxy), nameof(PlayerShipProxy.LoadShipType))]
    internal static class PortSubmarineLoader
    {
        private static void Prefix(ref string scenePath, ref string shipType)
        {
            // The details view supplies the player entity; the selector can supply its display type.
            var definition = SubmarineRegistry.FindByEntityType(shipType);
            if (definition == null && shipType != null && scenePath == shipType + " Interior" &&
                !shipType.EndsWith(" (Player)", StringComparison.Ordinal))
            {
                definition = SubmarineRegistry.FindByEntityType(shipType + " (Player)");
            }

            if (definition != null && (scenePath != definition.InteriorScene || shipType != definition.EntityType))
            {
                SubmarineLoading.ValidateAssets(definition);
                scenePath = definition.InteriorScene;
                shipType = definition.EntityType;
            }
        }
    }

    [HarmonyPatch(typeof(ShipSelectorUI), "LoadItems")]
    internal static class PortSubmarineOptions
    {
        [Inject]
        private static PlayerCareer career = null;

        private static void Postfix(ShipSelectorItemUI ___itemPrefab, RectTransform ___itemContainer)
        {
            foreach (var definition in SubmarineRegistry.Definitions())
            {
                if (definition.PortShip == null || !SubmarineLoading.HasAssets(definition))
                {
                    continue;
                }

                bool unlocked = career.UnlockedShips.Contains(definition.PortType);
                bool eligible = definition.PortPrerequisite == null ||
                    career.UnlockedShips.Contains(definition.PortPrerequisite);
                var state = unlocked ? ShipSelectorItemState.Unlocked :
                    eligible ? ShipSelectorItemState.AvailableToUnlock : ShipSelectorItemState.Locked;
                ___itemPrefab.Duplicate(definition.PortShip, definition.PortType, 1000, ___itemContainer, state);
            }
        }
    }
}
