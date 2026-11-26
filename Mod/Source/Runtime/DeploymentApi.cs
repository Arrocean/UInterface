using System;

namespace UInterface
{
    public static class DeploymentApi
    {
        public const int ApiVersion = 1;

        private sealed class Binding
        {
            internal readonly Func<bool, bool> Request;
            internal readonly Func<string> ReadState;

            internal Binding(Func<bool, bool> request, Func<string> readState)
            {
                Request = request;
                ReadState = readState;
            }
        }

        private static readonly ShipBindings<Binding> Equipment = new ShipBindings<Binding>();

        public static IDisposable RegisterDeployable(string equipmentId, Func<bool, bool> request, Func<string> readState)
        {
            return request != null && readState != null && SubmarineApi.TryGetEquipmentMount(equipmentId, out var mount)
                ? Equipment.Register(equipmentId, new Binding(request, readState), mount)
                : null;
        }

        public static bool RequestDeployment(string equipmentId, bool deploy)
        {
            return TryGetAvailableEquipment(equipmentId, out var binding) && binding.Request(deploy);
        }

        public static bool TryGetEquipmentState(string equipmentId, out string state)
        {
            state = null;
            if (!TryGetAvailableEquipment(equipmentId, out var binding))
            {
                return false;
            }

            string current = binding.ReadState();
            if (string.IsNullOrWhiteSpace(current))
            {
                return false;
            }

            state = current;
            return true;
        }

        private static bool TryGetAvailableEquipment(string equipmentId, out Binding binding)
        {
            return Equipment.TryGet(equipmentId, out binding, out var mount) &&
                SubmarineApi.TryGetEquipmentMount(equipmentId, out var resolved) && resolved == mount;
        }
    }
}
