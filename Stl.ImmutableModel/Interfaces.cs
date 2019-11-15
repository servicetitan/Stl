using System.Collections.Generic;
using Stl.Extensibility;

namespace Stl.ImmutableModel
{
    public interface INode : IFreezable
    {
        Key Key { get; set; }
        bool HasKey { get; }
        Symbol LocalKey { get; }
    }

    public interface ISimpleNode : INode, IHasOptions
    {}

    public interface ICollectionNode : INode
    {
        IEnumerable<Symbol> Keys { get; }
        IEnumerable<object?> Values { get; }
        IEnumerable<KeyValuePair<Symbol, object?>> Items { get; }
        
        object? this[Symbol key] { get; set; }
        bool ContainsKey(Symbol key);
        bool TryGetValue(Symbol key, out object? value);
        void Add(Symbol key, object? value);
        bool Remove(Symbol key);
        void Clear();
    }
    
    public interface ICollectionNode<T> : ICollectionNode, IDictionary<Symbol, T>
    {}
}
