using System;
using System.Collections.Generic;

namespace Stl.Extensibility
{
    public readonly struct Owned<TItem, TOwner> : IDisposable, IEquatable<Owned<TItem, TOwner>>
        where TOwner : IDisposable
    {
        public TItem Item { get; }
        public TOwner Owner { get; }
        
        public Owned(TItem item, TOwner owner)
        {
            Item = item;
            Owner = owner;
        }

        public void Deconstruct(out TItem item, out TOwner owner)
        {
            item = Item;
            owner = Owner;
        }

        public override string ToString() => $"{GetType().Name}({Item} @ {Owner})";

        public void Dispose() => Owner?.Dispose();

        // Equality

        public bool Equals(Owned<TItem, TOwner> other) 
            => EqualityComparer<TItem>.Default.Equals(Item, other.Item) 
                && EqualityComparer<TOwner>.Default.Equals(Owner, other.Owner);
        public override bool Equals(object? obj) => obj is Owned<TItem, TOwner> other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Item, Owner);
        public static bool operator ==(Owned<TItem, TOwner> left, Owned<TItem, TOwner> right) => left.Equals(right);
        public static bool operator !=(Owned<TItem, TOwner> left, Owned<TItem, TOwner> right) => !left.Equals(right);
    }
}
