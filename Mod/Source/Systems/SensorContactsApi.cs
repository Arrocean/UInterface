using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UInterface
{
    public static class SensorContactsApi
    {
        public const int ApiVersion = 1;

        private static readonly Dictionary<string, Source> Sources = new Dictionary<string, Source>(StringComparer.Ordinal);

        public static event Action<string, string, bool> OnContactChanged;

        public static bool TryReportContact(string sensorId, object provider, string contactId,
            float bearing, Vector2? sandboxPosition)
        {
            if (!SubmarineRegistry.IsIdentifier(contactId) || !IsFinite(bearing) ||
                (sandboxPosition.HasValue && (!IsFinite(sandboxPosition.Value.x) || !IsFinite(sandboxPosition.Value.y))) ||
                !SensorsApi.TryGetSensor(sensorId, typeof(object), out var active) || !ReferenceEquals(active, provider))
            {
                return false;
            }

            if (!Sources.TryGetValue(sensorId, out var source) || !ReferenceEquals(source.Provider, active))
            {
                source = new Source(active);
                Sources[sensorId] = source;
            }

            float normalized = bearing % 360f;
            source.Contacts[contactId] = new Contact(normalized < 0f ? normalized + 360f : normalized, sandboxPosition);
            Notify(sensorId, contactId, true);
            return true;
        }

        public static bool TryRemoveContact(string sensorId, object provider, string contactId)
        {
            if (!SubmarineRegistry.IsIdentifier(contactId) || !TryGetSource(sensorId, out var source) ||
                !ReferenceEquals(source.Provider, provider) || !source.Contacts.Remove(contactId))
            {
                return false;
            }

            Notify(sensorId, contactId, false);
            return true;
        }

        public static string[] GetContactSources()
        {
            return SensorsApi.GetRegisteredSensorIds().Where(id => TryGetSource(id, out var source) &&
                source.Contacts.Count > 0).ToArray();
        }

        public static string[] GetContactIds(string sensorId)
        {
            return TryGetSource(sensorId, out var source) ? source.Contacts.Keys.ToArray() : Array.Empty<string>();
        }

        public static bool TryGetContact(string sensorId, string contactId, out float bearing, out Vector2? sandboxPosition)
        {
            bearing = 0f;
            sandboxPosition = null;
            if (!SubmarineRegistry.IsIdentifier(contactId) || !TryGetSource(sensorId, out var source) ||
                !source.Contacts.TryGetValue(contactId, out var contact))
            {
                return false;
            }

            bearing = contact.Bearing;
            sandboxPosition = contact.Position;
            return true;
        }

        private static bool TryGetSource(string sensorId, out Source source)
        {
            source = null;
            return SensorsApi.TryGetSensor(sensorId, typeof(object), out var provider) &&
                Sources.TryGetValue(sensorId, out source) && ReferenceEquals(source.Provider, provider);
        }

        internal static void ForgetSensor(string sensorId)
        {
            if (Sources.TryGetValue(sensorId, out var source))
            {
                Sources.Remove(sensorId);
                foreach (string contactId in source.Contacts.Keys)
                {
                    Notify(sensorId, contactId, false);
                }
            }
        }

        private static void Notify(string sensorId, string contactId, bool available)
        {
            var handlers = OnContactChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<string, string, bool> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(sensorId, contactId, available);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsInfinity(value) && !float.IsNaN(value);
        }

        private sealed class Source
        {
            internal readonly object Provider;
            internal readonly Dictionary<string, Contact> Contacts = new Dictionary<string, Contact>(StringComparer.Ordinal);

            internal Source(object provider)
            {
                Provider = provider;
            }
        }

        private struct Contact
        {
            internal readonly float Bearing;
            internal readonly Vector2? Position;

            internal Contact(float bearing, Vector2? position)
            {
                Bearing = bearing;
                Position = position;
            }
        }
    }
}
