using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;

namespace UInterface
{
    internal static class NativeLaunchSpread
    {
        private static readonly ConditionalWeakTable<IEnumerator, Request> Launches =
            new ConditionalWeakTable<IEnumerator, Request>();
        private static Request pending;

        internal static bool TryLaunch(PlayerShip ship, float depth, float speed, float spread)
        {
            if (!SubmarineFramework.TryInstallLaunchSpread())
            {
                return false;
            }

            var previous = pending;
            var request = new Request(ship, spread);
            pending = request;
            try
            {
                ship.LaunchAllTorpedoes(depth, speed, spread);
                return request.Attached;
            }
            finally
            {
                pending = previous;
            }
        }

        internal static void Attach(PlayerShip ship, IEnumerator launch)
        {
            var request = pending;
            if (request != null && !request.Attached && request.Ship == ship && launch != null)
            {
                Launches.Add(launch, request);
                request.Attached = true;
            }
        }

        internal static float Read(TorpedoLauncherGroup group, IEnumerator launch)
        {
            return Launches.TryGetValue(launch, out var request) ? request.Spread : group.SetSalvoSpreadAngle;
        }

        private sealed class Request
        {
            internal readonly PlayerShip Ship;
            internal readonly float Spread;
            internal bool Attached;

            internal Request(PlayerShip ship, float spread)
            {
                Ship = ship;
                Spread = spread;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "LaunchAllTorpedoesInternal", typeof(float), typeof(float), typeof(float), typeof(float))]
    internal static class NativeLaunchRequest
    {
        private static void Postfix(PlayerShip __instance, IEnumerator __result)
        {
            NativeLaunchSpread.Attach(__instance, __result);
        }
    }

    [HarmonyPatch]
    internal static class NativeLaunchSpreadRead
    {
        private static MethodBase TargetMethod()
        {
            var launch = AccessTools.DeclaredMethod(typeof(PlayerShip), "LaunchAllTorpedoesInternal",
                new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
            return AccessTools.EnumeratorMoveNext(launch);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var getter = AccessTools.PropertyGetter(typeof(TorpedoLauncherGroup), nameof(TorpedoLauncherGroup.SetSalvoSpreadAngle));
            int index = code.FindIndex(instruction => instruction.Calls(getter));
            if (index < 0 || index != code.FindLastIndex(instruction => instruction.Calls(getter)))
            {
                throw new InvalidOperationException("The native launch spread read could not be identified.");
            }

            // Keep the group on the stack and pass this launch iterator as the second argument.
            code[index].opcode = OpCodes.Ldarg_0;
            code[index].operand = null;
            var read = AccessTools.DeclaredMethod(typeof(NativeLaunchSpread), nameof(NativeLaunchSpread.Read));
            code.Insert(index + 1, new CodeInstruction(OpCodes.Call, read));
            return code;
        }
    }
}
