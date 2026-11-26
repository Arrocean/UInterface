using System;
using HarmonyLib;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace UInterface
{
    internal static class AmmoCompatibility
    {
        private static readonly ShipBindings<Rule> Rules = new ShipBindings<Rule>();

        internal static IDisposable Register(string ammoType, Func<ExtendedTorpedoLauncher, bool> acceptsTube)
        {
            if (acceptsTube == null || !SubmarineRegistry.IsIdentifier(ammoType) ||
                !SubmarineApi.TryGetReadySubmarine(out _, out _) || !SubmarineFramework.TryInstallAmmoCompatibility())
            {
                return null;
            }

            return Rules.Register(ammoType, new Rule(acceptsTube));
        }

        internal static bool Allows(TorpedoLauncherGroup group, int index, StoredTorpedo ammo)
        {
            if (!IsCurrentGroup(group) || !Rules.TryGet(ammo.TypeName, out var rule))
            {
                return true;
            }

            var tubes = group.Launchers;
            return index >= 0 && index < tubes.Length && tubes[index] && tubes[index].Group == group &&
                rule.Allows(tubes[index]);
        }

        internal static bool TrySelectAutomaticLoad(TorpedoLauncherGroup group, out int tubeIndex, out int ammoIndex)
        {
            tubeIndex = -1;
            ammoIndex = -1;
            if (!IsCurrentGroup(group) || !group.TorpedoStorage || !HasRuleForStock(group.TorpedoStorage))
            {
                return false;
            }

            var source = group.TorpedoStorage;
            var tubes = group.Launchers;
            for (int i = 0; i < tubes.Length && i < group.SlotCount; i++)
            {
                if (!TorpedoTubeApi.CanLoad(tubes[i], group, i))
                {
                    continue;
                }

                int candidate = FindAmmo(group, i, source);
                if (candidate >= 0)
                {
                    tubeIndex = i;
                    ammoIndex = candidate;
                    break;
                }
            }

            return true;
        }

        private static bool IsCurrentGroup(TorpedoLauncherGroup group)
        {
            return Rules.HasEntries && group && group.Launchers != null &&
                SubmarineApi.TryGetReadySubmarine(out _, out var ship) && group.ParentEntity == ship;
        }

        private static bool HasRuleForStock(Storage source)
        {
            for (int i = 0; i < source.SlotCount; i++)
            {
                if (source.GetItemAt(i) is StoredTorpedo item && item && Rules.TryGet(item.TypeName, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindAmmo(TorpedoLauncherGroup group, int tubeIndex, Storage source)
        {
            int fallback = -1;
            for (int i = source.SlotCount - 1; i >= 0; i--)
            {
                if (!(source.GetItemAt(i) is StoredTorpedo item) || source.HasTransportOrders(i) ||
                    (fallback >= 0 && !(item is StoredMinePacket)) ||
                    !TorpedoTubeApi.IsCompatible(item, group, tubeIndex))
                {
                    continue;
                }

                if (item is StoredMinePacket)
                {
                    return i;
                }

                fallback = i;
            }

            return fallback;
        }

        private sealed class Rule
        {
            private readonly Func<ExtendedTorpedoLauncher, bool> accepts;
            private bool reportedFailure;

            internal Rule(Func<ExtendedTorpedoLauncher, bool> accepts)
            {
                this.accepts = accepts;
            }

            internal bool Allows(ExtendedTorpedoLauncher tube)
            {
                try
                {
                    return accepts(tube);
                }
                catch (Exception exception)
                {
                    if (!reportedFailure)
                    {
                        reportedFailure = true;
                        Debug.LogException(exception);
                    }

                    return false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Storage), nameof(Storage.IsItemAllowed))]
    internal static class NativeAmmoSlotRule
    {
        private static void Postfix(Storage __instance, int __0, IEquipment __1, ref bool __result)
        {
            if (__result && __instance is TorpedoLauncherGroup group && __1 is StoredTorpedo ammo && ammo)
            {
                __result = AmmoCompatibility.Allows(group, __0, ammo);
            }
        }
    }

    [HarmonyPatch(typeof(LoadTorpedoesAction), "GetPreferredLauncherToLoadInto")]
    internal static class NativeAutomaticLoadTube
    {
        private static bool Prefix(TorpedoLauncherGroup __0, ref int __result)
        {
            if (!AmmoCompatibility.TrySelectAutomaticLoad(__0, out int tube, out _))
            {
                return true;
            }

            __result = tube;
            return false;
        }
    }

    [HarmonyPatch(typeof(LoadTorpedoesAction), "GetPreferredTorpedoToLoad")]
    internal static class NativeAutomaticLoadAmmo
    {
        private static bool Prefix(TorpedoLauncherGroup __0, ref int __result)
        {
            if (!AmmoCompatibility.TrySelectAutomaticLoad(__0, out _, out int ammo))
            {
                return true;
            }

            __result = ammo;
            return false;
        }
    }
}
