using System;

namespace UInterface
{
    public static class SubsystemApi
    {
        public const int ApiVersion = 1;

        private static readonly ShipBindings<object> Providers = new ShipBindings<object>();

        public static IDisposable RegisterProvider(string subsystemId, object provider)
        {
            return provider is UnityEngine.Object unityObject && !unityObject
                ? null
                : Providers.Register(subsystemId, provider);
        }

        public static bool TryGetProvider(string subsystemId, Type contract, out object provider)
        {
            provider = null;
            if (contract == null || !Providers.TryGet(subsystemId, out var candidate) ||
                !contract.IsInstanceOfType(candidate) ||
                (candidate is UnityEngine.Object unityObject && !unityObject))
            {
                return false;
            }

            provider = candidate;
            return true;
        }
    }
}
