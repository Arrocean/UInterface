using System;
using System.Collections.Generic;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using UBOAT.Game.Scene.Characters.Skills;
using UnityEngine;
using ShipActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace UInterface
{
    internal static class StationTemplates
    {
        private static readonly ShipBindings<Binding> Bindings = new ShipBindings<Binding>();

        internal static IDisposable Register(string actionId, ShipActivator station, CharacterAction template)
        {
            if (template == null || !SubmarineRegistry.IsIdentifier(actionId) ||
                !UboatStationAdapter.IsCurrentStation(station) ||
                !SubmarineApi.TryGetReadySubmarine(out _, out _) ||
                (template.Activator && template.Activator != station) ||
                !SubmarineFramework.TryInstallStationTemplates())
            {
                return null;
            }

            return Bindings.Register(actionId, new Binding(station, template), station.transform);
        }

        internal static void Append(List<CharacterActionInfo> result, ShipActivator station,
            PlayableCharacter character, ActionContext context, bool isPlayerOrder)
        {
            if (!Bindings.HasEntries || !UboatStationAdapter.IsCurrentStation(station))
            {
                return;
            }

            foreach (string id in Bindings.GetIds())
            {
                if (!Bindings.TryGet(id, out var binding) || binding.Station != station ||
                    result.Exists(info => ReferenceEquals(info.ActionTemplate, binding.Template)))
                {
                    continue;
                }

                try
                {
                    var state = binding.Template.GetState(station, character, fillDetails: false,
                        isPlayerOrder: isPlayerOrder, actionContext: context).ActionState;
                    if (state != ActionState.Hidden)
                    {
                        result.Add(new CharacterActionInfo(binding.Template, state));
                    }
                }
                catch (Exception exception)
                {
                    if (!binding.ReportedFailure)
                    {
                        binding.ReportedFailure = true;
                        Debug.LogException(exception);
                    }
                }
            }
        }

        private sealed class Binding
        {
            internal readonly ShipActivator Station;
            internal readonly CharacterAction Template;
            internal bool ReportedFailure;

            internal Binding(ShipActivator station, CharacterAction template)
            {
                Station = station;
                Template = template;
            }
        }
    }

    [HarmonyPatch(typeof(ShipActivator), nameof(ShipActivator.GetActionInfos),
        typeof(PlayableCharacter), typeof(ActionContext), typeof(bool))]
    internal static class StationTemplateQuery
    {
        private static void Postfix(ShipActivator __instance, PlayableCharacter __0, ActionContext __1, bool __2,
            List<CharacterActionInfo> __result)
        {
            StationTemplates.Append(__result, __instance, __0, __1, __2);
        }
    }

    [HarmonyPatch(typeof(ShipActivator), nameof(ShipActivator.GetActionInfos),
        typeof(List<CharacterActionInfo>), typeof(PlayableCharacter), typeof(ActionContext), typeof(bool))]
    internal static class StationTemplateListQuery
    {
        private static void Postfix(ShipActivator __instance, List<CharacterActionInfo> __0,
            PlayableCharacter __1, ActionContext __2, bool __3)
        {
            StationTemplates.Append(__0, __instance, __1, __2, __3);
        }
    }
}
