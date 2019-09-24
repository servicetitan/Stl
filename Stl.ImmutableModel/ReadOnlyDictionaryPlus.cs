using System.Collections;
using System.Collections.Generic;

namespace Stl.ImmutableModel
{
    public interface IReadOnlyDictionaryPlus<TKey>
        where TKey : notnull
    {
        IEnumerable<TKey> Keys { get; }

        bool ContainsKey(TKey key);
        bool TryGetValueUntyped(TKey key, out object? value);
    }

    // ReSharper disable once PossibleInterfaceMemberAmbiguity
    public interface IReadOnlyDictionaryPlus<TKey, TValue> 
        : IReadOnlyDictionaryPlus<TKey>, IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    { }
    
    public sealed class ReadOnlyDictionaryPlus<TKey, TValue> : IReadOnlyDictionaryPlus<TKey, TValue>
        where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, TValue> _source;

        public int Count => _source.Count;
        public IEnumerable<TKey> Keys => _source.Keys;
        public IEnumerable<TValue> Values => _source.Values;
        public TValue this[TKey key] => _source[key];

        public ReadOnlyDictionaryPlus(IReadOnlyDictionary<TKey, TValue> source) => _source = source;

        public bool ContainsKey(TKey key) => _source.ContainsKey(key);

        public bool TryGetValue(TKey key, out TValue value) => _source.TryGetValue(key, out value);
        public bool TryGetValueUntyped(TKey key, out object? value)
        {
            if (_source.TryGetValue(key, out var v)) {
                // ReSharper disable once HeapView.BoxingAllocation
                value = v;
                return true;
            }
            value = null;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _source.GetEnumerator();
    }
}