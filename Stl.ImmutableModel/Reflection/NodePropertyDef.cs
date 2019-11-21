using System;
using System.Reflection;
using Stl.ImmutableModel.Internal;
using Stl.Reflection;
using Stl.Text;

namespace Stl.ImmutableModel.Reflection
{
    public interface INodePropertyDef
    {
        Type Type { get; }
        Symbol PropertyName { get; }
        PropertyInfo PropertyInfo { get; }
        public bool IsNode { get; } 
        public bool IsSimpleNode { get; } 
        public bool IsCollectionNode { get; } 
        public bool IsFreezable { get; } 
        public bool MayBeNode { get; } 
        public bool MayBeFreezable { get; } 

        MethodInfo? GetterInfo { get; }
        MethodInfo? SetterInfo { get; }
        Delegate? Getter { get; }
        Delegate? UntypedGetter { get; }
        Delegate? Setter { get; }
        Delegate? UntypedSetter { get; }
    }

    public interface INodePropertyDef<T> : INodePropertyDef
    {
        new Func<ISimpleNode, T>? Getter { get; }
        new Func<ISimpleNode, object?>? UntypedGetter { get; }
        new Action<ISimpleNode, T>? Setter { get; }
        new Action<ISimpleNode, object?>? UntypedSetter { get; }
    }
    
    public class NodePropertyDef<T> : INodePropertyDef<T>
    {
        public Type Type { get; }
        public Symbol PropertyName { get; }
        public PropertyInfo PropertyInfo { get; }

        public bool IsFreezable { get; }
        public bool IsNode { get; }
        public bool IsSimpleNode { get; }
        public bool IsCollectionNode { get; }

        public bool MayBeFreezable { get; }
        public bool MayBeNode { get; }
        public bool MayBeSimpleNode { get; }
        public bool MayBeCollectionNode { get; }

        public MethodInfo? GetterInfo { get; }
        public MethodInfo? SetterInfo { get; }
        Delegate? INodePropertyDef.Getter => Getter;
        Delegate? INodePropertyDef.UntypedGetter => UntypedGetter;
        Delegate? INodePropertyDef.Setter => Setter;
        Delegate? INodePropertyDef.UntypedSetter => UntypedSetter;
        public Func<ISimpleNode, T>? Getter { get; }
        public Func<ISimpleNode, object?>? UntypedGetter { get; }
        public Action<ISimpleNode, T>? Setter { get; }
        public Action<ISimpleNode, object?>? UntypedSetter { get; }

        public NodePropertyDef(Type type, Symbol propertyName)
        {
            Type = type;
            PropertyName = propertyName;
            PropertyInfo = PropertyEx.GetProperty(type, propertyName) 
                           ?? throw Errors.PropertyNotFound(type, propertyName);

            IsFreezable = typeof(IFreezable).IsAssignableFrom(type); 
            IsNode = typeof(NodeBase).IsAssignableFrom(type); 
            IsSimpleNode = typeof(SimpleNodeBase).IsAssignableFrom(type); 
            IsCollectionNode = typeof(CollectionNodeBase).IsAssignableFrom(type); 

            MayBeFreezable = type.MayCastSucceed(typeof(IFreezable));
            MayBeNode = type.MayCastSucceed(typeof(NodeBase));
            MayBeSimpleNode = type.MayCastSucceed(typeof(SimpleNodeBase));
            MayBeCollectionNode = type.MayCastSucceed(typeof(CollectionNodeBase));

            GetterInfo = PropertyInfo.GetGetMethod();
            SetterInfo = PropertyInfo.GetSetMethod();
            Getter = (Func<ISimpleNode, T>?) type.GetGetter(propertyName, false);
            UntypedGetter = (Func<ISimpleNode, object?>?) type.GetGetter(propertyName, true);
            Setter = (Action<ISimpleNode, T>?) type.GetSetter(propertyName, false);
            UntypedSetter = (Action<ISimpleNode, object?>?) type.GetSetter(propertyName, true);
        }
    }
}
