namespace Stl.ImmutableModel 
{
    public static class ReadOnlyDictionaryPlusEx
    {
        public static Option<object?> GetValueUntyped<TKey>(this IReadOnlyDictionaryPlus<TKey> source, TKey key)
            where TKey : notnull
            => source.TryGetValueUntyped(key, out var v) ? Option.Some(v) : default; 
    }
}
