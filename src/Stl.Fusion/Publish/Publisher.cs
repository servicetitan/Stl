using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Channels;
using Stl.Fusion.Publish.Internal;
using Stl.Fusion.Publish.Messages;
using Stl.OS;
using Stl.Reflection;
using Stl.Security;
using Stl.Text;

namespace Stl.Fusion.Publish
{
    public interface IPublisher
    {
        Symbol Id { get; }
        IPublication Publish(IComputed computed, Type? publicationType = null);
        IPublication? TryGet(Symbol publicationId);
    }

    public interface IPublisherImpl : IPublisher
    {
        IChannelHub<Message> ChannelHub { get; }
        bool Subscribe(Channel<Message> channel, IPublication publication, bool notify);
        ValueTask<bool> UnsubscribeAsync(Channel<Message> channel, IPublication publication);
        void OnPublicationDisposed(IPublication publication);
    }

    public abstract class PublisherBase : AsyncDisposableBase, IPublisherImpl
    {
        protected ConcurrentDictionary<(ComputedInput Input, Type PublicationType), IPublication> Publications { get; } 
        protected ConcurrentDictionary<Symbol, IPublication> PublicationsById { get; }
        protected ConcurrentDictionary<Channel<Message>, ChannelProcessor> ChannelProcessors { get; }
        protected IGenerator<Symbol> PublicationIdGenerator { get; }
        protected bool OwnsChannelRegistry { get; }
        protected Action<Channel<Message>> OnChannelAttachedCached { get; } 
        protected Func<Channel<Message>, ValueTask> OnChannelDetachedCached { get; } 

        public Symbol Id { get; }
        public IChannelHub<Message> ChannelHub { get; }
        public IPublicationFactory PublicationFactory { get; }
        public Type DefaultPublicationType { get; }

        protected PublisherBase(Symbol id, 
            IChannelHub<Message> channelHub,
            IGenerator<Symbol> publicationIdGenerator,
            bool ownsChannelRegistry = true,
            IPublicationFactory? publicationFactory = null,
            Type? defaultPublicationType = null)
        {
            publicationFactory ??= Fusion.Publish.PublicationFactory.Instance;
            defaultPublicationType ??= typeof(UpdatingPublication<>);
            Id = id;
            ChannelHub = channelHub;
            OwnsChannelRegistry = ownsChannelRegistry;
            PublicationIdGenerator = publicationIdGenerator;
            PublicationFactory = publicationFactory;
            DefaultPublicationType = defaultPublicationType;

            var concurrencyLevel = HardwareInfo.ProcessorCount << 2;
            var capacity = 7919;
            Publications = new ConcurrentDictionary<(ComputedInput, Type), IPublication>(concurrencyLevel, capacity);
            PublicationsById = new ConcurrentDictionary<Symbol, IPublication>(concurrencyLevel, capacity);
            ChannelProcessors = new ConcurrentDictionary<Channel<Message>, ChannelProcessor>(concurrencyLevel, capacity);

            OnChannelAttachedCached = OnChannelAttached;
            OnChannelDetachedCached = OnChannelDetachedAsync;
            ChannelHub.Detached += OnChannelDetachedCached; // Must go first
            ChannelHub.Attached += OnChannelAttachedCached;
        }

        public virtual IPublication Publish(IComputed computed, Type? publicationType = null)
        {
            ThrowIfDisposedOrDisposing();
            publicationType ??= DefaultPublicationType;
            var spinWait = new SpinWait();
            while (true) {
                 var p = Publications.GetOrAddChecked(
                    (computed.Input, PublicationType: publicationType), 
                    (key, arg) => {
                        var (this1, computed1) = arg;
                        var publicationType1 = key.PublicationType;
                        var id = this1.PublicationIdGenerator.Next();
                        var p1 = this1.PublicationFactory.Create(publicationType1, this1, computed1, id);
                        this1.PublicationsById[id] = p1;
                        ((IPublicationImpl) p1).RunAsync();
                        return p1;
                    }, (this, computed));
                if (p.Touch())
                    return p;
                spinWait.SpinOnce();
            }
        }

        public virtual IPublication? TryGet(Symbol publicationId) 
            => PublicationsById.TryGetValue(publicationId, out var p) ? p : null;

        void IPublisherImpl.OnPublicationDisposed(IPublication publication) 
            => OnPublicationDisposed(publication);
        protected virtual void OnPublicationDisposed(IPublication publication)
        {
            if (publication.Publisher != this)
                throw new ArgumentOutOfRangeException(nameof(publication));
            if (!PublicationsById.TryGetValue(publication.Id, out var p))
                return;
            Publications.TryRemove((p.Computed.Input, p.PublicationType), p);
            PublicationsById.TryRemove(p.Id, p);
        }


        // Channel-related

        protected virtual ChannelProcessor CreateChannelProcessor(Channel<Message> channel) 
            => new ChannelProcessor(channel, this);

        protected virtual void OnChannelAttached(Channel<Message> channel)
        {
            var channelProcessor = CreateChannelProcessor(channel);
            if (!ChannelProcessors.TryAdd(channel, channelProcessor))
                return;
            channelProcessor.RunAsync().ContinueWith(_ => {
                // Since ChannelProcessor is AsyncProcessorBase desc.,
                // its disposal will shut down RunAsync as well,
                // so "subscribing" to RunAsync completion is the
                // same as subscribing to its disposal.
                ChannelProcessors.TryRemove(channel, channelProcessor);
            });
        }

        protected virtual ValueTask OnChannelDetachedAsync(Channel<Message> channel)
        {
            if (!ChannelProcessors.TryGetValue(channel, out var channelProcessor))
                return ValueTaskEx.CompletedTask;
            return channelProcessor.DisposeAsync();
        }

        bool IPublisherImpl.Subscribe(Channel<Message> channel, IPublication publication, bool notify) 
            => Subscribe(channel, publication, notify);
        protected bool Subscribe(Channel<Message> channel, IPublication publication, bool notify)
        {
            ThrowIfDisposedOrDisposing();
            if (!ChannelProcessors.TryGetValue(channel, out var channelProcessor))
                return false;
            if (publication.Publisher != this || publication.State == PublicationState.Unpublished)
                return false;
            return channelProcessor.Subscribe(publication, notify);
        }

        ValueTask<bool> IPublisherImpl.UnsubscribeAsync(Channel<Message> channel, IPublication publication) 
            => UnsubscribeAsync(channel, publication);
        protected ValueTask<bool> UnsubscribeAsync(Channel<Message> channel, IPublication publication)
        {
            if (!ChannelProcessors.TryGetValue(channel, out var channelProcessor))
                return ValueTaskEx.FalseTask;
            return channelProcessor.UnsubscribeAsync(publication);
        }

        protected override async ValueTask DisposeInternalAsync(bool disposing)
        {
            ChannelHub.Attached -= OnChannelAttachedCached;
            var publications = PublicationsById;
            while (!publications.IsEmpty) {
                var tasks = publications
                    .Take(HardwareInfo.ProcessorCount * 4)
                    .ToList()
                    .Select(p => Task.Run(async () => {
                        var (_, publication) = (p.Key, p.Value);
                        await publication.DisposeAsync().ConfigureAwait(false);
                    }));
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            if (OwnsChannelRegistry)
                await ChannelHub.DisposeAsync().ConfigureAwait(false);
            await base.DisposeInternalAsync(disposing).ConfigureAwait(false);
        }
    }
}
