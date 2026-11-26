using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UInterface
{
    internal sealed class ShipBindings<T> where T : class
    {
        private sealed class Entry
        {
            internal readonly T Value;
            internal readonly long Serial;
            internal readonly Transform Mount;
            internal readonly int? SceneHandle;

            internal Entry(T value, long serial, Transform mount)
            {
                Value = value;
                Serial = serial;
                Mount = mount;
                SceneHandle = mount ? (int?)mount.gameObject.scene.handle : null;
            }
        }

        private sealed class Registration : IDisposable
        {
            private ShipBindings<T> bindings;
            private readonly string id;
            private readonly long serial;

            internal Registration(ShipBindings<T> bindings, string id, long serial)
            {
                this.bindings = bindings;
                this.id = id;
                this.serial = serial;
            }

            public void Dispose()
            {
                bindings?.Remove(id, serial);
                bindings = null;
            }
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Action<string, bool> availabilityChanged;
        private readonly Func<T, bool> isAlive;
        private readonly Action<T> onRemoved;
        private long nextSerial;

        internal bool HasEntries => entries.Count > 0;

        internal ShipBindings(Action<string, bool> availabilityChanged = null, Func<T, bool> isAlive = null,
            Action<T> onRemoved = null)
        {
            this.availabilityChanged = availabilityChanged;
            this.isAlive = isAlive;
            this.onRemoved = onRemoved;
            PlayerSubmarineContext.Invalidated += Clear;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        internal IDisposable Register(string id, T value, Transform mount = null)
        {
            if (!SubmarineRegistry.IsIdentifier(id) || value == null || !HasActiveContext())
            {
                return null;
            }

            if (entries.TryGetValue(id, out var existing) && IsAvailable(existing))
            {
                return null;
            }

            long serial = ++nextSerial;
            entries[id] = new Entry(value, serial, mount);
            if (existing != null)
            {
                onRemoved?.Invoke(existing.Value);
            }

            availabilityChanged?.Invoke(id, true);
            return new Registration(this, id, serial);
        }

        internal string[] GetIds()
        {
            if (!HasActiveContext())
            {
                return Array.Empty<string>();
            }

            var ids = new string[entries.Count];
            entries.Keys.CopyTo(ids, 0);
            return ids;
        }

        internal bool TryGet(string id, out T value)
        {
            return TryGet(id, out value, out _);
        }

        internal TResult AggregateAvailable<TResult>(TResult result, Func<TResult, T, TResult> accumulate)
        {
            if (HasActiveContext())
            {
                foreach (var entry in entries.Values)
                {
                    if (IsAvailable(entry))
                    {
                        result = accumulate(result, entry.Value);
                    }
                }
            }

            return result;
        }

        internal bool TryGet(string id, out T value, out Transform mount)
        {
            value = null;
            mount = null;
            if (!SubmarineRegistry.IsIdentifier(id) ||
                !HasActiveContext() || !entries.TryGetValue(id, out var entry))
            {
                return false;
            }

            if (!IsAvailable(entry))
            {
                Remove(id, entry.Serial);
                return false;
            }

            value = entry.Value;
            mount = entry.Mount;
            return true;
        }

        private bool IsAvailable(Entry entry)
        {
            return (!entry.SceneHandle.HasValue || entry.Mount) &&
                (isAlive == null || isAlive(entry.Value));
        }

        private void Clear()
        {
            var removed = availabilityChanged != null ? new List<string>(entries.Keys) : null;
            var values = onRemoved != null ? new List<Entry>(entries.Values) : null;
            entries.Clear();
            if (values != null && values.Count > 0)
            {
                PlayerSubmarineContext.PublishAfterInvalidation(() =>
                {
                    foreach (var entry in values)
                    {
                        onRemoved(entry.Value);
                    }
                });
            }

            if (removed != null && removed.Count > 0)
            {
                PlayerSubmarineContext.PublishAfterInvalidation(() =>
                {
                    foreach (string id in removed)
                    {
                        if (!entries.ContainsKey(id))
                        {
                            availabilityChanged(id, false);
                        }
                    }
                });
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            RemoveMatching(entry => entry.SceneHandle == scene.handle);
        }

        internal void SweepUnavailable()
        {
            RemoveMatching(entry => !IsAvailable(entry));
        }

        private void RemoveMatching(Predicate<Entry> matches)
        {
            List<KeyValuePair<string, Entry>> removed = null;
            foreach (var entry in entries)
            {
                if (matches(entry.Value))
                {
                    if (removed == null)
                    {
                        removed = new List<KeyValuePair<string, Entry>>();
                    }

                    removed.Add(entry);
                }
            }

            if (removed != null)
            {
                foreach (var entry in removed)
                {
                    Remove(entry.Key, entry.Value.Serial);
                }
            }
        }

        private void Remove(string id, long serial)
        {
            if (entries.TryGetValue(id, out var entry) && entry.Serial == serial)
            {
                entries.Remove(id);
                onRemoved?.Invoke(entry.Value);
                availabilityChanged?.Invoke(id, false);
            }
        }

        private static bool HasActiveContext()
        {
            return PlayerSubmarineReadiness.CanBind &&
                SubmarineApi.TryGetCurrentSubmarine(out _, out _);
        }
    }
}
