using Stl.Collections;
using Stl.ImmutableModel.Reflection;

namespace Stl.ImmutableModel
{
    public static class NodeEx
    {
        public static NodeTypeDef GetDefinition(this INode node) 
            => NodeTypeDef.Get(node.GetType());
        
        public static void FindChildren<TChild>(this INode root, ListBuffer<TChild> output, bool includeRoot = false)
            where TChild : class, INode
        {
            if (root is TChild n && includeRoot) {
                output.Add(n);
                return;
            }

            using var bufferLease = ListBuffer<INode>.Rent();
            var buffer = bufferLease.Buffer;

            root.GetDefinition().GetNodeItems(root, buffer);
            foreach (var node in buffer)
                FindChildren(node, output, true);
        }
    }
}
