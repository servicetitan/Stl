using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stl;
using Stl.ImmutableModel.Internal;

namespace Stl.ImmutableModel
{
    [Serializable]
//    [JsonConverter(typeof(ChangeSetJsonConverter))]
    public readonly struct ChangeSet : IEquatable<ChangeSet>, ISerializable
    {
        public static ChangeSet Empty { get; } = new ChangeSet(ImmutableDictionary<DomainKey, ChangeKind>.Empty);

        private readonly ChangeSetDeserializationHelper? _deserializationHelper;
        private readonly ImmutableDictionary<DomainKey, ChangeKind>? _changes;

        public ImmutableDictionary<DomainKey, ChangeKind> Changes {
            get {
                if (_changes != null)
                    return _changes;
                var changes = _deserializationHelper?.GetImmutableDictionary()
                    ?? ImmutableDictionary<DomainKey, ChangeKind>.Empty;
                // Tricky: the struct is readonly (and ideally, must be);
                // the code below tries to overwrite it to fix the deserialization
                // + make sure the conversion from Dictionary to ImmutableDictionary
                // happens just once.
                ref var r = ref Unsafe.AsRef(this);
                r = new ChangeSet(changes);
                return changes;
            }
        }

        [JsonConstructor]
        public ChangeSet(ImmutableDictionary<DomainKey, ChangeKind> changes)
        {
            _deserializationHelper = null;
            _changes = changes;
        }

        public override string ToString() => $"{GetType()}({Changes.Count} change(s))";

        public ChangeSet Add(DomainKey domainKey, ChangeKind changeKind)
        {
            changeKind = Changes.GetValueOrDefault(domainKey) | changeKind;
            return changeKind == 0 ? this : new ChangeSet(Changes.SetItem(domainKey, changeKind));
        }

        public ChangeSet Merge(ChangeSet other)
            => other.Changes.Aggregate(this, (s, p) => s.Add(p.Key, p.Value));

        // Operators

        public static ChangeSet operator +(ChangeSet first, ChangeSet second) => first.Merge(second);

        // Equality

        public bool Equals(ChangeSet other) => Equals(Changes, other.Changes);
        public override bool Equals(object? obj) => obj is ChangeSet other && Equals(other);
        public override int GetHashCode() => Changes.GetHashCode();
        public static bool operator ==(ChangeSet left, ChangeSet right) => left.Equals(right);
        public static bool operator !=(ChangeSet left, ChangeSet right) => !left.Equals(right);

        // Serialization

        private ChangeSet(SerializationInfo info, StreamingContext context)
        {
            var changes = info.GetValue(nameof(Changes), typeof(object))!;
            switch (changes) {
                case JObject j:
                    _deserializationHelper = null;
                    _changes = j.ToObject<Dictionary<DomainKey, ChangeKind>>().ToImmutableDictionary();
                    break;
                case Dictionary<DomainKey, ChangeKind> d: 
                    _deserializationHelper = new ChangeSetDeserializationHelper(d);
                    _changes = null;
                    break;
                default:
                    throw new SerializationException();
            }
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(Changes), Changes.ToDictionary());
        }
    }
}
