using System;
using System.Collections.Generic;
using System.Text;

namespace Gloub.AI.GOAP
{
    /// <summary>
    /// Sparse world-state map. Get() treats a missing key as 0 (false).
    /// Zero values may be stored so action effects can explicitly clear a flag
    /// (e.g. HasFood := 0 after eating). Equality treats missing and 0 as the same.
    /// </summary>
    public sealed class WorldState : IEquatable<WorldState>
    {
        readonly Dictionary<string, int> _values = new Dictionary<string, int>(12);

        public int Count => _values.Count;

        public int Get(string key)
        {
            return _values.TryGetValue(key, out var v) ? v : 0;
        }

        public bool GetBool(string key) => Get(key) != 0;

        public void Set(string key, int value)
        {
            _values[key] = value;
        }

        public void Set(string key, bool value) => Set(key, value ? 1 : 0);

        public IEnumerable<KeyValuePair<string, int>> Entries => _values;

        public bool Satisfies(WorldState desired)
        {
            if (desired == null) return true;
            foreach (var kv in desired._values)
            {
                if (Get(kv.Key) != kv.Value) return false;
            }
            return true;
        }

        public WorldState Clone()
        {
            var copy = new WorldState();
            foreach (var kv in _values) copy._values[kv.Key] = kv.Value;
            return copy;
        }

        public WorldState WithApplied(WorldState effects)
        {
            var next = Clone();
            if (effects == null) return next;
            foreach (var kv in effects._values) next._values[kv.Key] = kv.Value;
            return next;
        }

        public bool Equals(WorldState other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            foreach (var kv in _values)
            {
                if (kv.Value == 0) continue;
                if (other.Get(kv.Key) != kv.Value) return false;
            }
            foreach (var kv in other._values)
            {
                if (kv.Value == 0) continue;
                if (Get(kv.Key) != kv.Value) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as WorldState);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var kv in _values)
                {
                    if (kv.Value == 0) continue;
                    hash += kv.Key.GetHashCode() * 397 ^ kv.Value;
                }
                return hash;
            }
        }

        public override string ToString()
        {
            if (_values.Count == 0) return "{}";
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in _values)
            {
                if (kv.Value == 0) continue;
                if (!first) sb.Append(' ');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }
    }

    public static class GoapFacts
    {
        public const string HasFood = "HasFood";
        public const string AtWater = "AtWater";
        public const string AtBush = "AtBush";
        public const string AtHome = "AtHome";
        public const string NearFriend = "NearFriend";
        public const string FruitAvailable = "FruitAvailable";
        public const string IsHydrated = "IsHydrated";
        public const string IsFed = "IsFed";
        public const string IsRested = "IsRested";
        public const string IsSocial = "IsSocial";
        public const string Idled = "Idled";

        public static void SetExclusiveLocation(WorldState state, string locationKey)
        {
            state.Set(AtWater, locationKey == AtWater);
            state.Set(AtBush, locationKey == AtBush);
            state.Set(AtHome, locationKey == AtHome);
            state.Set(NearFriend, locationKey == NearFriend);
        }
    }
}
