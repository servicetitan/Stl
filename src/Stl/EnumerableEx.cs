using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Stl.Internal;

namespace Stl
{
    public static class EnumerableEx
    {
        // Regular static methods

        public static IEnumerable<T> One<T>(T value) => Enumerable.Repeat(value, 1);

        public static IEnumerable<T> Concat<T>(params IEnumerable<T>[] sequences)
        {
            if (sequences.Length == 0)
                return Enumerable.Empty<T>();
            var result = sequences[0];
            for (var i = 1; i < sequences.Length; i++) 
                result = result.Concat(sequences[i]);
            return result;
        }

        // Extensions

        public static IEnumerable<T> AsEnumerable<T>(this ReadOnlyMemory<T> source)
        {
            for (var i = 0; i < source.Length; i++)
                yield return source.Span[i];
        }

        public static IEnumerable<T> InsertAfter<T>(this IEnumerable<T> source, Func<T, bool> predicate, IEnumerable<T> insertion)
        {
            foreach (var item in source) {
                yield return item;
                if (predicate.Invoke(item)) {
                    foreach (var extraItem in insertion)
                        yield return extraItem;
                }
            }
        }

        public static IEnumerable<T> InsertBefore<T>(this IEnumerable<T> source, Func<T, bool> predicate, IEnumerable<T> insertion)
        {
            foreach (var item in source) {
                if (predicate.Invoke(item)) {
                    foreach (var extraItem in insertion)
                        yield return extraItem;
                }
                yield return item;
            }
        }

        public static IEnumerable<T> InsertOnceAfter<T>(this IEnumerable<T> source, Func<T, bool> predicate, IEnumerable<T> insertion)
        {
            var mustInsert = true;
            foreach (var item in source) {
                yield return item;
                if (mustInsert && predicate.Invoke(item)) {
                    mustInsert = false;
                    foreach (var extraItem in insertion)
                        yield return extraItem;
                }
            }
        }

        public static IEnumerable<T> InsertOnceBefore<T>(this IEnumerable<T> source, Func<T, bool> predicate, IEnumerable<T> insertion)
        {
            var mustInsert = true;
            foreach (var item in source) {
                if (mustInsert && predicate.Invoke(item)) {
                    mustInsert = false;
                    foreach (var extraItem in insertion)
                        yield return extraItem;
                }
                yield return item;
            }
        }

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source)
            where TKey : notnull
            => source.ToDictionary(p => p.Key, p => p.Value);

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<(TKey Key, TValue Value)> source)
            where TKey : notnull
            => source.ToDictionary(p => p.Key, p => p.Value);

        public static bool TryRemove<TKey, TValue>(
            this ConcurrentDictionary<TKey, TValue> dictionary, 
            TKey key, TValue value)
            where TKey : notnull
            // Based on:
            // - https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
            => ((ICollection<KeyValuePair<TKey, TValue>>) dictionary).Remove(
                KeyValuePair.Create(key, value));

        public static string ToDelimitedString<T>(this IEnumerable<T> source, string? delimiter = null)
            => string.Join(delimiter ?? ", ", source);

        public static IEnumerable<T> OrderByDependency<T>(
            this IEnumerable<T> source, 
            Func<T, IEnumerable<T>> dependencySelector)
        {
            var processing = new HashSet<T>();
            var processed = new HashSet<T>();
            var stack = new Stack<T>(source);

            while (stack.TryPop(out var item)) {
                if (processed.Contains(item))
                    continue;
                if (processing.Contains(item)) {
                    processing.Remove(item);
                    processed.Add(item);
                    yield return item;
                    continue;
                }
                processing.Add(item);
                stack.Push(item); // Pushing item in advance assuming there are dependencies
                var stackSize = stack.Count;
                foreach (var dependency in dependencySelector(item))
                    if (!processed.Contains(dependency)) {
                        if (processing.Contains(dependency))
                            throw Errors.CircularDependency(item);
                        stack.Push(dependency);
                    }
                if (stackSize == stack.Count) { // No unprocessed dependencies
                    stack.Pop(); // Popping item pushed in advance
                    processing.Remove(item);
                    processed.Add(item);
                    yield return item;
                }
            }
        }

        // ConcurrentDictionary helpers

        public static int Decrement<TKey>(this ConcurrentDictionary<TKey, int> dictionary, TKey key)
            where TKey : notnull
        {
            while (true) {
                var value = dictionary[key];
                if (value > 1) {
                    var newValue = value - 1;
                    if (dictionary.TryUpdate(key, newValue, value))
                        return newValue;
                }
                else {
                    if (dictionary.TryRemove(key, value))
                        return 0;
                }
            }
        }


        public static int Increment<TKey>(this ConcurrentDictionary<TKey, int> dictionary, TKey key)
            where TKey : notnull
        {
            while (true) {
                if (dictionary.TryGetValue(key, out var value)) {
                    var newValue = value + 1;
                    if (dictionary.TryUpdate(key, newValue, value))
                        return newValue;
                }
                else {
                    if (dictionary.TryAdd(key, 1))
                        return 1;
                }
            }
        }
    }
}