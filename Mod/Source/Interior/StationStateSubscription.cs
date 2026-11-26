using System;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShipActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace UInterface
{
    internal sealed class StationStateSubscription : IDisposable
    {
        private ShipActivator station;
        private Device device;
        private Action callback;
        private readonly int sceneHandle;

        internal StationStateSubscription(ShipActivator station, Action callback)
        {
            this.station = station;
            this.callback = callback;
            sceneHandle = station.gameObject.scene.handle;
            device = station.AssociatedEquipment as Device;
            if (device)
            {
                device.DisableFlagsChanged += OnDeviceFlagsChanged;
            }

            station.StateChanged += Notify;
            station.ActionStarted += OnAssignmentChanged;
            station.ActionStopped += OnAssignmentChanged;
            PlayerSubmarineContext.Invalidated += Dispose;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void Dispose()
        {
            if (ReferenceEquals(station, null))
            {
                return;
            }

            if (!ReferenceEquals(device, null))
            {
                device.DisableFlagsChanged -= OnDeviceFlagsChanged;
            }

            station.StateChanged -= Notify;
            station.ActionStarted -= OnAssignmentChanged;
            station.ActionStopped -= OnAssignmentChanged;
            PlayerSubmarineContext.Invalidated -= Dispose;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            station = null;
            device = null;
            callback = null;
        }

        private void Notify()
        {
            try
            {
                callback?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnDeviceFlagsChanged(Device changedDevice)
        {
            Notify();
        }

        private void OnAssignmentChanged(PlayableCharacter character)
        {
            Notify();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.handle == sceneHandle)
            {
                Dispose();
            }
        }
    }
}
