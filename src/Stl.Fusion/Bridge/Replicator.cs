using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Channels;
using Stl.Fusion.Bridge.Internal;
using Stl.Fusion.Bridge.Messages;
using Stl.OS;
using Stl.Text;

namespace Stl.Fusion.Bridge
{
    public interface IReplicator
    {
        IChannelHub<Message> ChannelHub { get; }
        bool OwnsChannelHub { get; }

        IReplica<T> GetOrAdd<T>(Symbol publisherId, Symbol publicationId, 
            LTagged<Result<T>> initialOutput, bool isConsistent = true, bool requestUpdate = false);
        IReplica? TryGet(Symbol publicationId);
    }

    public interface IReplicatorImpl : IReplicator
    {
        IComputeRetryPolicy RetryPolicy { get; }

        bool TrySubscribe(IReplica replica, bool requestUpdate);
        void OnReplicaDisposed(IReplica replica);
        void OnChannelProcessorDisposed(ReplicatorChannelProcessor replicatorChannelProcessor);
    }

    public class Replicator : AsyncDisposableBase, IReplicatorImpl
    {
        protected static readonly Func<Channel<Message>, Symbol> DefaultPublisherIdProvider =
            c => c is IHasId<Symbol> hasId ? hasId.Id : Symbol.Empty; 
        protected ConcurrentDictionary<Symbol, IReplica> Replicas { get; }
        protected ConcurrentDictionary<Channel<Message>, ReplicatorChannelProcessor> ChannelProcessors { get; }
        protected ConcurrentDictionary<Symbol, ReplicatorChannelProcessor> ChannelProcessorsById { get; }
        protected Action<Channel<Message>> OnChannelAttachedHandler { get; } 
        protected Func<Channel<Message>, ValueTask> OnChannelDetachedAsyncHandler { get; } 

        public IChannelHub<Message> ChannelHub { get; }
        public Func<Channel<Message>, Symbol> PublisherIdProvider { get; }
        public IComputeRetryPolicy RetryPolicy { get; }
        public bool OwnsChannelHub { get; }

        public Replicator(
            IChannelHub<Message>? channelHub = null,
            Func<Channel<Message>, Symbol>? publisherIdProvider = null,
            IComputeRetryPolicy? retryPolicy = null,
            bool ownsChannelHub = true)
        {
            channelHub ??= new ChannelHub<Message>();
            publisherIdProvider ??= DefaultPublisherIdProvider;
            retryPolicy ??= ComputeRetryPolicy.Default;

            ChannelHub = channelHub;
            PublisherIdProvider = publisherIdProvider;
            RetryPolicy = retryPolicy;
            OwnsChannelHub = ownsChannelHub;
            Replicas = new ConcurrentDictionary<Symbol, IReplica>();
            ChannelProcessorsById = new ConcurrentDictionary<Symbol, ReplicatorChannelProcessor>();
            ChannelProcessors = new ConcurrentDictionary<Channel<Message>, ReplicatorChannelProcessor>();
            
            OnChannelAttachedHandler = OnChannelAttached;
            OnChannelDetachedAsyncHandler = OnChannelDetachedAsync;
            ChannelHub.Detached += OnChannelDetachedAsyncHandler; // Must go first
            ChannelHub.Attached += OnChannelAttachedHandler;
        }

        public virtual IReplica<T> GetOrAdd<T>(Symbol publisherId, Symbol publicationId, 
            LTagged<Result<T>> initialOutput, bool isConsistent = true, bool requestUpdate = false)
        {
            var spinWait = new SpinWait();
            IReplica? replica; 
            while (!Replicas.TryGetValue(publicationId, out replica)) {
                replica = new Replica<T>(this, publisherId, publicationId, initialOutput, isConsistent, requestUpdate);
                if (Replicas.TryAdd(publicationId, replica))
                    TrySubscribe(replica, requestUpdate);
                spinWait.SpinOnce();
            }
            return (IReplica<T>) replica;
        }

        public virtual IReplica? TryGet(Symbol publicationId) 
            => Replicas.TryGetValue(publicationId, out var replica) ? replica : null;

        protected virtual void OnChannelAttached(Channel<Message> channel)
        {
            var publisherId = PublisherIdProvider.Invoke(channel);
            var channelProcessor = CreateChannelProcessor(channel, publisherId);
            if (!ChannelProcessors.TryAdd(channel, channelProcessor))
                return;
            while (!ChannelProcessorsById.TryAdd(publisherId, channelProcessor)) {
                if (ChannelProcessorsById.TryRemove(publisherId, out var oldChannelProcessor)) {
                    // We intend to just start the task here
                    oldChannelProcessor.DisposeAsync();  
                }
            }
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
            ChannelProcessorsById.TryRemove(channelProcessor.PublisherId, channelProcessor);
            return channelProcessor.DisposeAsync();
        }

        protected virtual ReplicatorChannelProcessor CreateChannelProcessor(
            Channel<Message> channel, Symbol publisherId) 
            => new ReplicatorChannelProcessor(this, channel, publisherId);

        bool IReplicatorImpl.TrySubscribe(IReplica replica, bool requestUpdate) 
            => TrySubscribe(replica, requestUpdate);
        protected virtual bool TrySubscribe(IReplica replica, bool requestUpdate)
        {
            if (replica.Replicator != this)
                throw new ArgumentOutOfRangeException(nameof(replica));
            if (!ChannelProcessorsById.TryGetValue(replica.PublisherId, out var channelProcessor))
                return false;
            // We intend to just start the task here
            channelProcessor.SubscribeAsync(replica, requestUpdate, default);
            return true;
        }

        void IReplicatorImpl.OnReplicaDisposed(IReplica replica) 
            => OnReplicaDisposed(replica);
        protected virtual void OnReplicaDisposed(IReplica replica)
        {
            if (replica.Replicator != this)
                throw new ArgumentOutOfRangeException(nameof(replica));
            Replicas.TryRemove(replica.PublicationId, replica);
        }

        void IReplicatorImpl.OnChannelProcessorDisposed(ReplicatorChannelProcessor channelProcessor) 
            => OnChannelProcessorDisposed(channelProcessor);
        protected virtual void OnChannelProcessorDisposed(ReplicatorChannelProcessor channelProcessor) 
            => ChannelProcessorsById.TryRemove(channelProcessor.PublisherId, channelProcessor);

        protected override async ValueTask DisposeInternalAsync(bool disposing)
        {
            ChannelHub.Attached -= OnChannelAttachedHandler; // Must go first
            ChannelHub.Detached -= OnChannelDetachedAsyncHandler;
            var channelProcessors = ChannelProcessors;
            while (!channelProcessors.IsEmpty) {
                var tasks = channelProcessors
                    .Take(HardwareInfo.ProcessorCount * 4)
                    .ToList()
                    .Select(p => {
                        var (_, channelProcessor) = (p.Key, p.Value);
                        return channelProcessor.DisposeAsync().AsTask();
                    });
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            await base.DisposeInternalAsync(disposing).ConfigureAwait(false);
        }
    }
}
