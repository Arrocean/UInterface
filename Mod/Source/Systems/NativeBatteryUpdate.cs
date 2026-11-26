using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UBOAT.Game.Core.Data;

namespace UInterface
{
    [HarmonyPatch(typeof(Resource), nameof(Resource.UpdateAmount), typeof(double), typeof(double))]
    internal static class NativeBatteryUpdate
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var sandboxGain = AccessTools.DeclaredField(typeof(Resource), "gainSandboxTimeScale");
            var readGain = AccessTools.DeclaredMethod(typeof(Parameter), "op_Implicit", new[] { typeof(Parameter) });
            int index = code.FindIndex(instruction => instruction.LoadsField(sandboxGain));
            if (index < 0 || index + 2 >= code.Count || !code[index + 1].Calls(readGain) ||
                code[index + 2].opcode != OpCodes.Conv_R8 ||
                index != code.FindLastIndex(instruction => instruction.LoadsField(sandboxGain)))
            {
                throw new InvalidOperationException("The native sandbox resource gain read could not be identified.");
            }

            // Keep the native integration and depletion logic. Pass the rate parameter and its resource.
            code[index + 1].opcode = OpCodes.Ldarg_0;
            code[index + 1].operand = null;
            code[index + 2].opcode = OpCodes.Call;
            code[index + 2].operand = AccessTools.DeclaredMethod(typeof(PowerApi), nameof(PowerApi.ResolveSandboxGain));
            return code;
        }
    }
}
