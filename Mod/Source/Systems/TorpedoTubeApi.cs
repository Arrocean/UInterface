using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using UBOAT.Game.Scene.Characters.Skills;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using ShipActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace UInterface
{
    public static class TorpedoTubeApi
    {
        public const int ApiVersion = 1;
        private static readonly ShipBindings<Func<ExtendedTorpedoLauncher, bool>> Compatibility =
            new ShipBindings<Func<ExtendedTorpedoLauncher, bool>>();

        public static ExtendedTorpedoLauncher[] GetTorpedoTubes()
        {
            if (!SubmarineApi.TryGetCurrentSubmarine(out _, out var ship) || ship.TorpedoLaunchers == null)
            {
                return Array.Empty<ExtendedTorpedoLauncher>();
            }

            return (ExtendedTorpedoLauncher[])ship.TorpedoLaunchers.Clone();
        }

        public static IDisposable RegisterAmmoCompatibility(string ammoType, Func<ExtendedTorpedoLauncher, bool> acceptsTube)
        {
            return SubmarineApi.TryGetReadySubmarine(out _, out _) ? Compatibility.Register(ammoType, acceptsTube) : null;
        }

        public static bool TryGetTubeState(ExtendedTorpedoLauncher tube, out StoredTorpedo ammo,
            out TorpedoLauncher.State state, out bool loaded, out bool flooded)
        {
            ammo = null;
            state = TorpedoLauncher.State.Empty;
            loaded = false;
            flooded = false;
            if (!TryGetCurrentTube(tube, out _, out _))
            {
                return false;
            }

            ammo = tube.Torpedo;
            state = tube.CurrentState;
            loaded = tube.IsLoaded;
            flooded = tube.IsFlooded;
            return true;
        }

        public static bool IsAmmoCompatible(ExtendedTorpedoLauncher tube, StoredTorpedo ammo)
        {
            return TryGetCurrentTube(tube, out var group, out int index) && IsCompatible(tube, ammo, group, index);
        }

        public static StoredTorpedo[] GetCompatibleStoredAmmo(ExtendedTorpedoLauncher tube)
        {
            if (!TryGetCurrentTube(tube, out var group, out int index) || !group.TorpedoStorage)
            {
                return Array.Empty<StoredTorpedo>();
            }

            var source = group.TorpedoStorage;
            var ammo = new List<StoredTorpedo>();
            for (int i = 0; i < source.SlotCount; i++)
            {
                if (source.GetItemAt(i) is StoredTorpedo item && IsCompatible(tube, item, group, index))
                {
                    ammo.Add(item);
                }
            }

            return ammo.ToArray();
        }

        public static async UniTask<CharacterAction> RequestNativeLoad(ExtendedTorpedoLauncher tube, StoredTorpedo ammo,
            PlayableCharacter actor, bool warm = false)
        {
            if (!TryGetCurrentTube(tube, out var group, out int targetIndex) ||
                !IsCompatible(tube, ammo, group, targetIndex) || !CanLoad(tube, group, targetIndex))
            {
                return null;
            }

            var source = group.TorpedoStorage;
            var station = group.GetComponentInParent<ShipActivator>();
            var queue = UboatStationAdapter.GetActionQueue(actor);
            int sourceIndex = source && ammo.Slot is ShipEquipmentSlot slot ? source.GetSlotIndex(slot) : -1;
            if (sourceIndex < 0 || !ReferenceEquals(source.GetItemAt(sourceIndex), ammo) ||
                source.HasTransportOrders(sourceIndex) || queue == null || !UboatStationAdapter.IsCurrentStation(station) ||
                station.AssociatedEquipment != group)
            {
                return null;
            }

            var order = new TransportOrder(ammo.Data, 1f, source, sourceIndex, group, targetIndex);
            var action = new LoadTorpedoesAction(station, order, warm) { IsDirectPlayerOrder = true };
            var context = actor ? ActionContext.Normal : ActionContext.Queue;
            int revision = PlayerSubmarineContext.Revision;
            if (action.GetState(station, actor, fillDetails: false, isPlayerOrder: true, actionContext: context).ActionState
                != ActionState.Enabled || !await action.OnPreStart(null) || revision != PlayerSubmarineContext.Revision ||
                !CanLoad(tube, group, targetIndex) || source.HasTransportOrders(sourceIndex) ||
                !ReferenceEquals(source.GetItemAt(sourceIndex), ammo))
            {
                return null;
            }

            try
            {
                order.Start();
                queue.AddPreStarted(action);
                if (queue.Contains(action))
                {
                    StationApi.TryTrackQueuedNativeAction(actor, action);
                    return action;
                }

                return action.Stage == CharacterActionStage.Completed ? action : null;
            }
            finally
            {
                if (!queue.Contains(action))
                {
                    order.Complete();
                }
            }
        }

        public static bool TryRequestNativeLaunch(float depth, float speed, float salvoSpread)
        {
            if (!(depth >= 0f) || float.IsInfinity(depth) || !(speed > 0f) || float.IsInfinity(speed) ||
                !(salvoSpread >= 0f) || float.IsInfinity(salvoSpread) ||
                !SubmarineApi.TryGetReadySubmarine(out _, out var ship) || ship.Docked ||
                ship.IsPreparingToLaunchTorpedoes || ship.IsLaunchingTorpedoes || ship.TorpedoLaunchers == null)
            {
                return false;
            }

            bool ready = false;
            foreach (var tube in ship.TorpedoLaunchers)
            {
                if (tube && tube.IsMarkedForLaunch && tube.IsFlooded)
                {
                    if (tube.IsBeingDestroyed || !tube.IsLoaded || tube.CurrentState != TorpedoLauncher.State.Flooded ||
                        !IsAmmoCompatible(tube, tube.Torpedo))
                    {
                        return false;
                    }

                    ready = true;
                }
            }

            if (ready)
            {
                ship.LaunchAllTorpedoes(depth, speed, salvoSpread);
            }

            return ready;
        }

        private static bool TryGetCurrentTube(ExtendedTorpedoLauncher tube, out TorpedoLauncherGroup group, out int index)
        {
            group = null;
            index = -1;
            if (!tube || !tube.Slot || tube.IsBeingDestroyed || !SubmarineApi.TryGetReadySubmarine(out _, out var ship) ||
                ship.TorpedoLaunchers == null || Array.IndexOf(ship.TorpedoLaunchers, tube) < 0 ||
                !tube.Group || tube.Group.ParentEntity != ship)
            {
                return false;
            }

            group = tube.Group;
            index = group.GetSlotIndex(tube.Slot);
            return index >= 0 && group.Launchers != null && index < group.Launchers.Length && group.Launchers[index] == tube;
        }

        private static bool IsCompatible(ExtendedTorpedoLauncher tube, StoredTorpedo ammo, TorpedoLauncherGroup group,
            int index)
        {
            return ammo && !ammo.IsBeingDestroyed && !ammo.IsLaunched && ammo.Data != null &&
                group.IsItemAllowed(index, ammo) &&
                (!Compatibility.TryGet(ammo.TypeName, out var acceptsTube) || acceptsTube(tube));
        }

        private static bool CanLoad(ExtendedTorpedoLauncher tube, TorpedoLauncherGroup group, int index)
        {
            return tube && !tube.IsBeingDestroyed && tube.CurrentState == TorpedoLauncher.State.Empty &&
                !tube.IsFlooded && !tube.Slot.Item && !group.HasTransportOrders(index) &&
                tube.GetAccessibilityStatus() == ExtendedTorpedoLauncher.TubeAccessStatus.Accessible &&
                tube.LoadingInteractable && tube.LoadingInteractable.IsOpenForAI;
        }
    }
}
