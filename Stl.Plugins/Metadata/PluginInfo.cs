using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json;
using Stl.Plugins.Internal;
using Stl.Reflection;

namespace Stl.Plugins.Metadata
{
    public class PluginInfo
    {
        public TypeRef Type { get; protected set; }
        public ImmutableArray<TypeRef> Ancestors { get; protected set; }
        public ImmutableArray<TypeRef> Interfaces { get; protected set; }
        public ImmutableHashSet<TypeRef> CastableTo { get; protected set; }
        public ImmutableDictionary<string, object> Capabilities { get; protected set; }
        public ImmutableHashSet<TypeRef> Dependencies { get; protected set; }
        public ImmutableHashSet<TypeRef> AllDependencies { get; protected set; }
        public int OrderByDependencyIndex { get; protected internal set; }

        [JsonConstructor]
        public PluginInfo(TypeRef type, 
            ImmutableArray<TypeRef> ancestors, 
            ImmutableArray<TypeRef> interfaces, 
            ImmutableHashSet<TypeRef> castableTo,
            ImmutableDictionary<string, object> capabilities,
            ImmutableHashSet<TypeRef> dependencies,
            ImmutableHashSet<TypeRef> allDependencies,
            int orderByDependencyIndex)
        {
            Type = type;
            Ancestors = ancestors;
            Interfaces = interfaces;
            CastableTo = castableTo;
            Capabilities = capabilities;
            Dependencies = dependencies;
            AllDependencies = allDependencies;
            OrderByDependencyIndex = orderByDependencyIndex;
        }

        public PluginInfo(Type type, PluginSetConstructionInfo constructionInfo)
        {
            Type = type;
            Ancestors = ImmutableArray.Create(
                type.GetAllBaseTypes().Select(t => (TypeRef) t).ToArray());
            Interfaces = ImmutableArray.Create(
                type.GetInterfaces().Select(t => (TypeRef) t).ToArray());
            CastableTo = ImmutableHashSet.Create(
                Ancestors.AddRange(Interfaces).Add(type).ToArray());
            Capabilities = ImmutableDictionary<string, object>.Empty;
            Dependencies = ImmutableHashSet<TypeRef>.Empty;

            object? tmpPlugin = null;
            try {
                tmpPlugin = constructionInfo.TemporaryPluginFactory!.Create(type);
            }
            catch (InvalidOperationException) {} // To ignore plugins we can't construct

            if (tmpPlugin is IHasCapabilities hc)
                Capabilities = hc.Capabilities;
            if (tmpPlugin is IHasDependencies hd)
                Dependencies = hd.Dependencies.Select(t => (TypeRef) t).ToImmutableHashSet();
            
            var allAssemblyRefs = constructionInfo.AllAssemblyRefs![type.Assembly];
            AllDependencies = constructionInfo.Plugins
                .Where(p => p != type && (
                    allAssemblyRefs.Contains(p.Assembly) || 
                    CastableTo.Contains(p)))
                .Select(t => (TypeRef) t)
                .Concat(Dependencies)
                .ToImmutableHashSet();
        }

        public override string ToString() => $"{GetType().Name}({Type})";
    }
}
