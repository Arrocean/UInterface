using System;
using System.Collections.Generic;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Data;
using UnityEngine;

namespace UInterface
{
    internal sealed class SubmarineDefinition
    {
        internal readonly string Id;
        internal readonly string BaseType;
        internal readonly string EntityType;
        internal readonly string InteriorScene;
        internal readonly string PortShip;
        internal readonly string PortPrerequisite;

        internal SubmarineDefinition(string id, string baseType, string entityType, string interiorScene,
            string portShip, string portPrerequisite)
        {
            Id = id;
            BaseType = baseType;
            EntityType = entityType;
            InteriorScene = interiorScene;
            PortShip = portShip;
            PortPrerequisite = portPrerequisite;
        }

        internal bool IsCustom => EntityType != null;
        internal string PortType => EntityType.Substring(0, EntityType.Length - " (Player)".Length);
    }

    internal static class SubmarineRegistry
    {
        private const string RegistryPath = "Sandbox/UInterfaceSubmarines";
        private const string PlayerSuffix = " (Player)";

        internal static readonly HashSet<string> VanillaTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Type IIA", "Type IID", "Type VIIB", "Type VIIC", "Type VIIC41",
            "Type IXA", "Type IXC", "Type IXC40"
        };

        private static readonly Dictionary<string, SubmarineDefinition> Registered =
            new Dictionary<string, SubmarineDefinition>(StringComparer.Ordinal);

        private static readonly HashSet<string> InvalidDefinitions = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> DuplicateEntities = new HashSet<string>(StringComparer.Ordinal);
        private static readonly char[] InvalidIdentifierCharacters = { '/', '\\', ':' };

        [Inject]
        private static GameData gameData = null;

        internal static GameDataRow ReadRow(string path)
        {
            return gameData?.GetDataRow(path);
        }

        internal static bool IsIdentifier(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && id == id.Trim() && id.IndexOfAny(InvalidIdentifierCharacters) < 0;
        }

        internal static bool Register(string id, string baseType, string entityType, string interiorScene)
        {
            if (!TryCreate(id, baseType, EmptyToNull(entityType), EmptyToNull(interiorScene), null, null,
                out var definition) || Registered.ContainsKey(id) || ReadRow(RegistryPath + "/" + id) != null)
            {
                return false;
            }

            if (definition.IsCustom)
            {
                foreach (var existing in Definitions())
                {
                    if (existing.EntityType == definition.EntityType)
                    {
                        return false;
                    }
                }
            }

            Registered.Add(id, definition);
            return true;
        }

        internal static SubmarineDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id) || VanillaTypes.Contains(id))
            {
                return null;
            }

            GameDataRow row = ReadRow(RegistryPath + "/" + id);
            if (row != null)
            {
                return ReadDefinition(row);
            }

            Registered.TryGetValue(id, out var definition);
            return definition;
        }

        internal static IEnumerable<SubmarineDefinition> Definitions()
        {
            if (gameData != null)
            {
                foreach (GameDataRow row in gameData.GetDataRows(RegistryPath))
                {
                    var definition = ReadDefinition(row);
                    if (definition != null)
                    {
                        yield return definition;
                    }
                }
            }

            foreach (var definition in Registered.Values)
            {
                if (ReadRow(RegistryPath + "/" + definition.Id) == null)
                {
                    yield return definition;
                }
            }
        }

        internal static SubmarineDefinition FindByEntityType(string entityType)
        {
            if (entityType == null || VanillaTypes.Contains(entityType) ||
                (entityType.EndsWith(PlayerSuffix, StringComparison.Ordinal) &&
                VanillaTypes.Contains(entityType.Substring(0, entityType.Length - PlayerSuffix.Length))))
            {
                return null;
            }

            SubmarineDefinition match = null;
            foreach (var definition in Definitions())
            {
                if (definition.EntityType != entityType)
                {
                    continue;
                }

                if (match != null)
                {
                    if (DuplicateEntities.Add(entityType))
                    {
                        Debug.LogError("[UInterface] Entity Type '" + entityType + "' belongs to both '" +
                            match.Id + "' and '" + definition.Id + "'.");
                    }

                    return null;
                }

                match = definition;
            }

            return match;
        }

        internal static string GetLoadedId(string entityType)
        {
            return FindByEntityType(entityType)?.Id ??
                (entityType != null && entityType.EndsWith(PlayerSuffix, StringComparison.Ordinal)
                    ? entityType.Substring(0, entityType.Length - PlayerSuffix.Length)
                    : entityType);
        }

        internal static string GetSceneName(string scene)
        {
            if (string.IsNullOrWhiteSpace(scene))
            {
                return null;
            }

            string name = scene.Substring(scene.LastIndexOfAny(new[] { '/', '\\' }) + 1);
            return name.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - ".unity".Length) : name;
        }

        internal static SubmarineDefinition FindByInteriorScene(string sceneName)
        {
            foreach (string vanillaType in VanillaTypes)
            {
                if (string.Equals(sceneName, vanillaType + " Interior", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            SubmarineDefinition match = null;
            foreach (var definition in Definitions())
            {
                if (!definition.IsCustom ||
                    !string.Equals(sceneName, GetSceneName(definition.InteriorScene), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = definition;
            }

            return match;
        }

        private static SubmarineDefinition ReadDefinition(GameDataRow row)
        {
            string id = row.GetString(0);
            if (TryCreate(id, row.GetString("Base Type"), EmptyToNull(row.GetString("Entity Type")),
                EmptyToNull(row.GetString("Interior Scene")), EmptyToNull(row.GetString("Port Ship")),
                EmptyToNull(row.GetString("Port Prerequisite")), out var definition))
            {
                return definition;
            }

            if (InvalidDefinitions.Add(row.Path))
            {
                Debug.LogError("[UInterface] Invalid submarine definition at " + row.Path + ".");
            }

            return null;
        }

        private static bool TryCreate(string id, string baseType, string entityType, string interiorScene,
            string portShip, string portPrerequisite, out SubmarineDefinition definition)
        {
            definition = null;
            if (!IsIdentifier(id) || VanillaTypes.Contains(id) || !VanillaTypes.Contains(baseType) ||
                (entityType == null) != (interiorScene == null))
            {
                return false;
            }

            if (entityType != null && (!entityType.EndsWith(PlayerSuffix, StringComparison.Ordinal) ||
                entityType.Length <= PlayerSuffix.Length ||
                VanillaTypes.Contains(entityType.Substring(0, entityType.Length - PlayerSuffix.Length))))
            {
                return false;
            }

            if (portShip != null && entityType == null)
            {
                return false;
            }

            definition = new SubmarineDefinition(id, baseType, entityType, interiorScene, portShip, portPrerequisite);
            return true;
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
