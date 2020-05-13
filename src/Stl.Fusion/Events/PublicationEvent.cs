using Stl.Time;

namespace Stl.Fusion.Events
{
    public abstract class PublicationEvent
    {
        public IPublication Publication { get; }
        public IntMoment HappenedAt { get; }

        protected PublicationEvent(IPublication publication)
        {
            Publication = publication;
            HappenedAt = IntMoment.Now;
        }
    }
}