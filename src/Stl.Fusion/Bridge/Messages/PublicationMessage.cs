using System;
using Newtonsoft.Json;
using Stl.Text;

namespace Stl.Fusion.Bridge.Messages
{
    [Serializable]
    public abstract class PublicationMessage
    {
        public Symbol PublisherId { get; set; }
        public Symbol PublicationId { get; set; }
        public long MessageIndex { get; set; }

        public override string ToString()
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            return $"{GetType().Name} {json}";
        }

    }
}
