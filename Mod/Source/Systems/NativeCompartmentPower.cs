using HarmonyLib;
using UBOAT.Game.Scene.Items;

namespace UInterface
{
    [HarmonyPatch(typeof(Compartment), nameof(Compartment.SetElectricityDisableReason))]
    internal static class NativeCompartmentPower
    {
        private static void Postfix(Compartment __instance)
        {
            PowerApi.OnCompartmentPowerChanged(__instance);
        }
    }
}
