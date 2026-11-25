using System;
using UnityEngine;

namespace UInterface
{
    public static class ControlApi
    {
        public const int ApiVersion = 1;

        private sealed class Binding
        {
            internal readonly string MountId;
            internal readonly Func<bool> Press;
            internal readonly Func<float> Read;
            internal readonly Func<float, bool> Set;

            internal Binding(string mountId, Func<bool> press, Func<float> read, Func<float, bool> set)
            {
                MountId = mountId;
                Press = press;
                Read = read;
                Set = set;
            }
        }

        private static readonly ShipBindings<Binding> Controls = new ShipBindings<Binding>();

        public static IDisposable RegisterButton(string controlId, string mountId, Func<bool> press)
        {
            return press != null && SubmarineApi.TryGetMount(mountId, out _)
                ? Controls.Register(controlId, new Binding(mountId, press, null, null))
                : null;
        }

        public static IDisposable RegisterAxis(string controlId, string mountId, Func<float> read, Func<float, bool> set)
        {
            return read != null && set != null && SubmarineApi.TryGetMount(mountId, out _)
                ? Controls.Register(controlId, new Binding(mountId, null, read, set))
                : null;
        }

        public static bool TryGetControlMount(string controlId, out Transform mount)
        {
            return TryGetAvailableControl(controlId, out _, out mount);
        }

        public static bool TryPress(string controlId)
        {
            return TryGetAvailableControl(controlId, out var binding, out _) &&
                binding.Press != null && binding.Press();
        }

        public static bool TryReadAxis(string controlId, out float value)
        {
            value = 0f;
            if (!TryGetAvailableControl(controlId, out var binding, out _) || binding.Read == null)
            {
                return false;
            }

            float current = binding.Read();
            if (float.IsInfinity(current) || float.IsNaN(current))
            {
                return false;
            }

            value = current;
            return true;
        }

        public static bool TrySetAxis(string controlId, float value)
        {
            return !float.IsInfinity(value) && !float.IsNaN(value) &&
                TryGetAvailableControl(controlId, out var binding, out _) && binding.Set != null && binding.Set(value);
        }

        private static bool TryGetAvailableControl(string controlId, out Binding binding, out Transform mount)
        {
            mount = null;
            return Controls.TryGet(controlId, out binding) && SubmarineApi.TryGetMount(binding.MountId, out mount);
        }
    }
}
