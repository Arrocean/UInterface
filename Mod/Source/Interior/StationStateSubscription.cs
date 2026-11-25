using System;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using ShipActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace UInterface
{
    internal sealed class StationStateSubscription : IDisposable
    {
        private ShipActivator station;
        private Device device;
        private Action callback;

        internal StationStateSubscription(ShipActivator station, Action callback)
        {
            this.station = station;
            this.callback = callback;
            device = station.AssociatedEquipment as Device;
            if (device)
            {
                device.DisableFlagsChanged += OnDeviceFlagsChanged;
            }

            station.StateChanged += Notify;
            station.ActionStarted += OnAssignmentChanged;
            station.ActionStopped += OnAssignmentChanged;
            SubmarineApi.OnPlayerSubmarineChanged += OnPlayerSubmarineChanged;
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
            SubmarineApi.OnPlayerSubmarineChanged -= OnPlayerSubmarineChanged;
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

        private void OnPlayerSubmarineChanged(PlayerShip previousShip, PlayerShip newShip)
        {
            Dispose();
        }
    }
}
