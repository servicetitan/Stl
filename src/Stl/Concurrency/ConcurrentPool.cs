using System;
using System.Collections.Concurrent;
using Stl.OS;
using Stl.Pooling;

namespace Stl.Concurrency
{
    public class ConcurrentPool<T> : IPool<T>
    {
        public static int DefaultCapacity => HardwareInfo.ProcessorCount << 5;

        private readonly StochasticCounter _count;
        private readonly ConcurrentBag<T> _pool;
        private readonly Func<T> _itemFactory;

        public int Capacity { get; }

        public ConcurrentPool(Func<T> itemFactory) 
            : this(itemFactory, DefaultCapacity) { }
        public ConcurrentPool(Func<T> itemFactory, int capacity) 
            : this(itemFactory, capacity, StochasticCounter.DefaultApproximationFactor) { }
        public ConcurrentPool(Func<T> itemFactory, int capacity, int counterApproximationFactor)
        {
            Capacity = capacity;
            _count = new StochasticCounter(counterApproximationFactor);
            _pool = new ConcurrentBag<T>();
            _itemFactory = itemFactory ?? throw new ArgumentNullException(nameof(itemFactory));
        }

        public ResourceLease<T> Rent()
        {
            if (_pool.TryTake(out var item)) {
                _count.Decrement(item!.GetHashCode(), out var _);
                return new ResourceLease<T>(item, this);
            }
            if (_count.ApproximateValue != 0)
                _count.ApproximateValue = 0;
            return new ResourceLease<T>(_itemFactory.Invoke(), this);
        }

        bool IResourceReleaser<T>.Release(T resource)
        {
            if (_count.ApproximateValue >= Capacity)
                return false;
            _count.Increment(resource!.GetHashCode(), out var _);
            _pool.Add(resource);
            return true;
        }
    }
}
