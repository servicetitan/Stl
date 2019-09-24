using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Stl.ImmutableModel.Internal;

namespace Stl.ImmutableModel
{
    public interface INode
    {
        Key Key { get; }
        Symbol LocalKey { get; }
    }

    [Serializable]
    [JsonConverter(typeof(NodeJsonConverter))]
    public abstract class NodeBase: INode, ISerializable
    {
        public virtual Key Key { get; protected set; }
        public Symbol LocalKey => Key.Parts.Tail;

        protected NodeBase(Key key) => Key = key;

        public override string ToString() => $"{GetType().Name}({Key})";
        
        // Serialization

        protected NodeBase(SerializationInfo info, StreamingContext context)
            : base()
        {
            Key = Key.Parse(info.GetString(nameof(Key)) ?? "");
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) 
            => GetObjectData(info, context);
        protected virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(Key), Key.Value);
        }
    }
}