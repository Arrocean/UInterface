using System;
using System.Collections.Generic;
using UBOAT.Game.Scene.Entities;

namespace UInterface
{
    internal sealed class ShipBindings<T> where T : class
    {
        private sealed class Entry
        {
            internal readonly T Value;
            internal readonly long Serial;

            internal Entry(T value, long serial)
            {
                Value = value;
                Serial = serial;
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
        private PlayerShip owner;
        private long nextSerial;

        internal ShipBindings()
        {
            SubmarineApi.OnPlayerSubmarineChanged += OnShipChanged;
        }

        internal IDisposable Register(string id, T value)
        {
            if (!SubmarineRegistry.IsIdentifier(id) || value == null ||
                !SyncOwner() || entries.ContainsKey(id))
            {
                return null;
            }

            long serial = ++nextSerial;
            entries.Add(id, new Entry(value, serial));
            return new Registration(this, id, serial);
        }

        internal bool TryGet(string id, out T value)
        {
            value = null;
            if (!SubmarineRegistry.IsIdentifier(id) ||
                !SyncOwner() || !entries.TryGetValue(id, out var entry))
            {
                return false;
            }

            value = entry.Value;
            return true;
        }

        private void Remove(string id, long serial)
        {
            if (entries.TryGetValue(id, out var entry) && entry.Serial == serial)
            {
                entries.Remove(id);
            }
        }

        private void OnShipChanged(PlayerShip previous, PlayerShip current)
        {
            SyncOwner();
        }

        private bool SyncOwner()
        {
            bool hasShip = SubmarineApi.TryGetCurrentSubmarine(out _, out var current);
            if (!hasShip)
            {
                current = null;
            }

            if (!ReferenceEquals(owner, current))
            {
                entries.Clear();
                owner = current;
            }

            return hasShip;
        }
    }
}
