using System;
using System.Collections.Generic;
using UnityEngine;

namespace UInterface
{
    public static class SubsystemApi
    {
        public const int ApiVersion = 1;

        private static readonly ShipBindings<object> Providers = new ShipBindings<object>(isAlive: IsProviderAlive);
        private static readonly ShipBindings<OperationSet> Operations =
            new ShipBindings<OperationSet>(NotifyOperationsAvailability);

        private sealed class OperationSet
        {
            internal readonly int Version;
            internal readonly Dictionary<string, Delegate> Methods;

            internal OperationSet(int version, Dictionary<string, Delegate> methods)
            {
                Version = version;
                Methods = methods;
            }
        }

        public static event Action<string, bool> OnOperationsAvailabilityChanged;

        public static IDisposable RegisterOperations(string subsystemId, int version,
            IReadOnlyDictionary<string, Delegate> operations)
        {
            return RegisterOperationSet(subsystemId, version, operations, null);
        }

        public static IDisposable RegisterMountedOperations(string subsystemId, string mountId, int version,
            IReadOnlyDictionary<string, Delegate> operations)
        {
            return SubmarineApi.TryGetMount(mountId, out var mount)
                ? RegisterOperationSet(subsystemId, version, operations, mount) : null;
        }

        private static IDisposable RegisterOperationSet(string subsystemId, int version,
            IReadOnlyDictionary<string, Delegate> operations, Transform mount)
        {
            if (version < 1 || operations == null || operations.Count == 0)
            {
                return null;
            }

            var snapshot = new Dictionary<string, Delegate>(StringComparer.Ordinal);
            foreach (var operation in operations)
            {
                if (!SubmarineRegistry.IsIdentifier(operation.Key) || operation.Value == null ||
                    snapshot.ContainsKey(operation.Key))
                {
                    return null;
                }

                snapshot.Add(operation.Key, operation.Value);
            }

            return Operations.Register(subsystemId, new OperationSet(version, snapshot), mount);
        }

        public static int GetOperationsVersion(string subsystemId)
        {
            return Operations.TryGet(subsystemId, out var operations) ? operations.Version : 0;
        }

        public static bool TryGetOperation(string subsystemId, int minimumVersion, string operationId,
            Type delegateType, out Delegate operation)
        {
            operation = null;
            if (minimumVersion < 1 || !SubmarineRegistry.IsIdentifier(operationId) || delegateType == null ||
                !typeof(Delegate).IsAssignableFrom(delegateType) || !Operations.TryGet(subsystemId, out var operations) ||
                operations.Version < minimumVersion || !operations.Methods.TryGetValue(operationId, out var method) ||
                !delegateType.IsInstanceOfType(method))
            {
                return false;
            }

            operation = method;
            return true;
        }

        private static void NotifyOperationsAvailability(string subsystemId, bool available)
        {
            var handlers = OnOperationsAvailabilityChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<string, bool> handler in handlers.GetInvocationList())
            {
                if ((GetOperationsVersion(subsystemId) > 0) != available)
                {
                    break;
                }

                try
                {
                    handler(subsystemId, available);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public static IDisposable RegisterProvider(string subsystemId, object provider)
        {
            return IsProviderAlive(provider) ? Providers.Register(subsystemId, provider) : null;
        }

        public static IDisposable RegisterMountedProvider(string subsystemId, string mountId, object provider)
        {
            return IsProviderAlive(provider) && SubmarineApi.TryGetMount(mountId, out var mount)
                ? Providers.Register(subsystemId, provider, mount) : null;
        }

        public static bool TryGetProvider(string subsystemId, Type contract, out object provider)
        {
            provider = null;
            if (contract == null || !Providers.TryGet(subsystemId, out var candidate) ||
                !contract.IsInstanceOfType(candidate))
            {
                return false;
            }

            provider = candidate;
            return true;
        }

        private static bool IsProviderAlive(object provider)
        {
            return provider != null && (!(provider is UnityEngine.Object unityObject) || unityObject);
        }
    }
}
