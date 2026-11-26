using System.Collections.Generic;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Environment;
using UnityEngine;

namespace UInterface
{
    internal static class ShipMountResolver
    {
        internal static Transform Find(PlayerShip ship, string path)
        {
            var mount = ship.Find(path);
            if (mount)
            {
                return mount;
            }

            foreach (var root in GetDependencyRoots(ship))
            {
                var candidate = root.Find(path);
                if (!candidate)
                {
                    continue;
                }

                if (mount && mount != candidate)
                {
                    return null;
                }

                mount = candidate;
            }

            return mount;
        }

        internal static IEnumerable<Transform> GetDependencyRoots(PlayerShip ship)
        {
            var roots = new List<Transform> { ship.transform };
            if (ship.Interior && ship.Interior.transform != ship.transform)
            {
                roots.Add(ship.Interior.transform);
            }

            var visited = new HashSet<Transform>(roots);
            for (int index = 0; index < roots.Count; index++)
            {
                foreach (var dependency in roots[index].GetComponentsInChildren<SceneDependency>(true))
                {
                    if (!dependency || dependency.ParentEntity != ship)
                    {
                        continue;
                    }

                    var root = dependency.SceneRootTransform;
                    if (root && root.gameObject.scene.isLoaded && visited.Add(root))
                    {
                        roots.Add(root);
                        yield return root;
                    }
                }
            }
        }
    }
}
