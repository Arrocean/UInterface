using System;
using System.Collections.Generic;
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

            var components = new List<Component>();
            var seen = new HashSet<Component>();
            var hull = ship.transform;
            var interior = ship.Interior ? ship.Interior.transform : null;
            if (interior && hull.IsChildOf(interior))
            {
                Collect(interior);
            }
            else
            {
                Collect(hull);
                if (interior && !interior.IsChildOf(hull))
                {
                    Collect(interior);
                }
            }

            foreach (var root in ShipMountResolver.GetDependencyRoots(ship))
            {
                Collect(root);
            }

            return components.ToArray();

            void Collect(Transform root)
            {
                foreach (var component in root.GetComponentsInChildren(componentType, includeInactive))
                {
                    if (component && seen.Add(component))
                    {
                        components.Add(component);
                    }
                }
            }
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
