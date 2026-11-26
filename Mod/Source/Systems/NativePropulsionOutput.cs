using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Entities;

namespace UInterface
{
    [HarmonyPatch(typeof(Ship), "Update")]
    internal static class NativePropellerPower
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var getPower = AccessTools.PropertyGetter(typeof(Engines), nameof(Engines.Power));
            var getValue = AccessTools.PropertyGetter(typeof(Parameter), nameof(Parameter.Value));
            int index = code.FindIndex(instruction => instruction.Calls(getPower));
            if (index < 0 || index + 1 >= code.Count || !code[index + 1].Calls(getValue) ||
                index != code.FindLastIndex(instruction => instruction.Calls(getPower)))
            {
                throw new InvalidOperationException("The native propeller power read could not be identified in Ship.Update.");
            }

            // The engine is already on the stack. Pass it and the ship to the resolver.
            code[index].opcode = OpCodes.Ldarg_0;
            code[index].operand = null;
            code[index + 1].opcode = OpCodes.Call;
            code[index + 1].operand = AccessTools.DeclaredMethod(typeof(PropulsionApi), nameof(PropulsionApi.ResolvePower));
            return code;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ValidateTargetVelocity")]
    internal static class NativeTargetSpeed
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var setSpeed = AccessTools.PropertySetter(typeof(Ship), nameof(Ship.TargetVelocity));
            var code = instructions.ToList();
            var setter = code.SingleOrDefault(instruction => instruction.Calls(setSpeed));
            if (setter == null)
            {
                throw new InvalidOperationException("The native target speed assignment could not be identified.");
            }

            setter.opcode = OpCodes.Call;
            setter.operand = AccessTools.DeclaredMethod(typeof(PropulsionApi), nameof(PropulsionApi.SetTargetSpeed));
            return code;
        }
    }
}
