using System;
using UnityEngine;

namespace UInterface
{
    public static class ComponentApi
    {
        public const int ApiVersion = 1;

        public static Component[] GetShipComponents(Type componentType, bool includeInactive = false)
        {
            if (!IsComponentType(componentType) || !SubmarineApi.TryGetCurrentSubmarine(out _, out var ship))
            {
                return Array.Empty<Component>();
            }

            var exterior = ship.GetComponentsInChildren(componentType, includeInactive);
            var interior = ship.Interior;
            if (!interior || interior.transform == ship.transform || interior.transform.IsChildOf(ship.transform))
            {
                return exterior;
            }

            var inside = interior.GetComponentsInChildren(componentType, includeInactive);
            if (ship.transform.IsChildOf(interior.transform))
            {
                return inside;
            }

            var combined = new Component[exterior.Length + inside.Length];
            Array.Copy(exterior, combined, exterior.Length);
            Array.Copy(inside, 0, combined, exterior.Length, inside.Length);
            return combined;
        }

        public static bool TryGetMountedComponent(string mountId, Type componentType, out Component component)
        {
            component = null;
            if (!IsComponentType(componentType) || !SubmarineApi.TryGetMount(mountId, out var mount))
            {
                return false;
            }

            var candidates = mount.GetComponents(componentType);
            if (candidates.Length != 1)
            {
                return false;
            }

            component = candidates[0];
            return component != null;
        }

        internal static bool TryGetUnique<T>(out T component) where T : Component
        {
            var candidates = GetShipComponents(typeof(T));
            component = candidates.Length == 1 ? candidates[0] as T : null;
            return component != null;
        }

        private static bool IsComponentType(Type type)
        {
            return type != null && !type.ContainsGenericParameters && typeof(Component).IsAssignableFrom(type);
        }
    }
}
