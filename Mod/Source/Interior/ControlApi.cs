using System;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;

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
            return press != null && SubmarineApi.TryGetMount(mountId, out var mount)
                ? Controls.Register(controlId, new Binding(mountId, press, null, null), mount)
                : null;
        }

        public static IDisposable RegisterFirstPersonButton(string controlId, string mountId,
            Func<PlayableCharacter, bool> press)
        {
            if (press == null || !SubmarineApi.TryGetMount(mountId, out var mount))
            {
                return null;
            }

            foreach (var interaction in mount.GetComponents<FPPInteractionObject>())
            {
                if (interaction && interaction.enabled)
                {
                    return null;
                }
            }

            var binding = new Binding(mountId,
                () => CrewServicesApi.TryGetFirstPersonCrew(out var character) && press(character), null, null);
            var registration = Controls.Register(controlId, binding, mount);
            if (registration == null)
            {
                return null;
            }

            try
            {
                var button = mount.gameObject.AddComponent<FirstPersonControlButton>();
                if (button)
                {
                    button.Bind(controlId, registration);
                    return button;
                }
            }
            catch
            {
                registration.Dispose();
                throw;
            }

            registration.Dispose();
            return null;
        }

        public static IDisposable RegisterAxis(string controlId, string mountId, Func<float> read, Func<float, bool> set)
        {
            return read != null && set != null && SubmarineApi.TryGetMount(mountId, out var mount)
                ? Controls.Register(controlId, new Binding(mountId, null, read, set), mount)
                : null;
        }

        public static IDisposable RegisterFirstPersonAxis(string controlId, string mountId, Func<float> read,
            Func<PlayableCharacter, float, bool> set, float unitsPerPixel, bool vertical = false)
        {
            if (read == null || set == null || unitsPerPixel == 0f || float.IsInfinity(unitsPerPixel) ||
                float.IsNaN(unitsPerPixel) || !TryGetAvailableFirstPersonMount(mountId, out var mount))
            {
                return null;
            }

            var registration = Controls.Register(controlId, new Binding(mountId, null, read, value =>
                CrewServicesApi.TryGetFirstPersonCrew(out var character) && set(character, value)), mount);
            if (registration == null)
            {
                return null;
            }

            try
            {
                var axis = mount.gameObject.AddComponent<FirstPersonControlAxis>();
                axis.Bind(controlId, registration, unitsPerPixel, vertical);
                return axis;
            }
            catch
            {
                registration.Dispose();
                throw;
            }
        }

        public static IDisposable RegisterFirstPersonHold(string controlId, string mountId,
            Action<PlayableCharacter, bool> changed)
        {
            if (changed == null || !TryGetAvailableFirstPersonMount(mountId, out var mount))
            {
                return null;
            }

            var registration = Controls.Register(controlId, new Binding(mountId, null, null, null), mount);
            if (registration == null)
            {
                return null;
            }

            try
            {
                var hold = mount.gameObject.AddComponent<FirstPersonControlHold>();
                hold.Bind(controlId, registration, changed);
                return hold;
            }
            catch
            {
                registration.Dispose();
                throw;
            }
        }

        private static bool TryGetAvailableFirstPersonMount(string mountId, out Transform mount)
        {
            if (!SubmarineApi.TryGetMount(mountId, out mount))
            {
                return false;
            }

            foreach (var component in mount.GetComponents<Behaviour>())
            {
                if (component && component.enabled && (component is IPointerClickHandler ||
                    component is IPointerDownHandler || component is IDragHandler))
                {
                    return false;
                }
            }

            return true;
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
            if (Controls.TryGet(controlId, out binding, out mount) &&
                SubmarineApi.TryGetMount(binding.MountId, out var resolved) && resolved == mount)
            {
                return true;
            }

            mount = null;
            return false;
        }
    }
}
