using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Entities;

namespace UInterface
{
    internal static class PlayerSubmarineReadiness
    {
        private static int? readyRevision;
        private static int? restoreRevision;

        internal static bool IsRestoring => restoreRevision.HasValue;
        internal static bool CanBind => !IsRestoring && !PlayerSubmarineContext.IsInvalidating;
        internal static bool IsReady => readyRevision == PlayerSubmarineContext.Revision;

        internal static void ObserveShip(PlayerShip ship)
        {
            if (ship && !IsRestoring)
            {
                AwaitReady(ship, PlayerSubmarineContext.Revision, restored: false).Forget();
            }
        }

        internal static int BeginRestore()
        {
            int revision = PlayerSubmarineContext.Revision + 1;
            restoreRevision = revision;
            DeviceStateApi.BeginRestore();
            PlayerSubmarineContext.Invalidate();
            return revision;
        }

        internal static void FinishRestore(int revision, bool restored)
        {
            if (restoreRevision != revision)
            {
                return;
            }

            restoreRevision = null;
            DeviceStateApi.FinishRestore(restored);
            if (restored && PlayerSubmarineContext.RefreshRestoredShip(out var ship))
            {
                AwaitReady(ship, PlayerSubmarineContext.Revision, restored: true).Forget();
            }
        }

        private static async UniTask AwaitReady(PlayerShip ship, int revision, bool restored)
        {
            var initialization = ship.InitializationComplete;
            await UniTask.WaitUntil(() => !ship || revision != PlayerSubmarineContext.Revision ||
                initialization.Status != UniTaskStatus.Pending);
            if (!ship || revision != PlayerSubmarineContext.Revision)
            {
                return;
            }

            await initialization;
            // Give other components in the activated scene time to finish their Start callbacks.
            await UniTask.NextFrame();
            if (ship && revision == PlayerSubmarineContext.Revision)
            {
                readyRevision = revision;
                SubmarineApi.NotifyReady(ship, restored, revision);
            }
        }
    }

    [HarmonyPatch(typeof(SavesManager), "LoadCoroutine", typeof(string))]
    internal static class SaveRestoreLifecycle
    {
        private static void Postfix(SavesManager __instance, ref IEnumerator __result)
        {
            __result = ObserveRestore(__instance, __result);
        }

        private static IEnumerator ObserveRestore(SavesManager manager, IEnumerator load)
        {
            int revision = PlayerSubmarineReadiness.BeginRestore();
            bool restored = false;
            try
            {
                while (load.MoveNext())
                {
                    yield return load.Current;
                }

                // The native failure exit leaves IsLoading set and quits the game.
                restored = !manager.IsLoading;
            }
            finally
            {
                (load as IDisposable)?.Dispose();
                PlayerSubmarineReadiness.FinishRestore(revision, restored);
            }
        }
    }
}
