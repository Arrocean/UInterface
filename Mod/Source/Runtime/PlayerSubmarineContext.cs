using System;
using System.Collections.Generic;
using UBOAT.Game.Scene.Entities;
using UnityEngine;

namespace UInterface
{
    internal static class PlayerSubmarineContext
    {
        private static List<Action> deferredNotifications;

        internal static event Action Invalidated;
        internal static int Revision { get; private set; }
        internal static PlayerShip Ship { get; private set; }
        internal static string SubmarineId { get; private set; }
        internal static int? SceneHandle { get; private set; }
        internal static bool IsInvalidating => deferredNotifications != null;

        internal static void SetShip(PlayerShip ship, string entityType)
        {
            Ship = ship;
            SubmarineId = ship ? SubmarineRegistry.GetLoadedId(entityType) : null;
            SceneHandle = ship ? (int?)ship.gameObject.scene.handle : null;
        }

        internal static bool RefreshRestoredShip(out PlayerShip ship)
        {
            ship = Ship;
            SetShip(ship, ship ? ship.SandboxEntity?.Type?.Name : null);
            return SubmarineId != null;
        }

        internal static int Invalidate()
        {
            int revision = ++Revision;
            var callbacks = new List<Action>();
            deferredNotifications = callbacks;
            try
            {
                Invalidated?.Invoke();
            }
            finally
            {
                deferredNotifications = null;
            }

            foreach (var callback in callbacks)
            {
                try
                {
                    callback();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            return revision;
        }

        internal static void PublishAfterInvalidation(Action callback)
        {
            if (deferredNotifications != null)
            {
                deferredNotifications.Add(callback);
            }
            else
            {
                callback();
            }
        }
    }
}
