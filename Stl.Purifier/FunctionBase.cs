using System;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Locking;
using Stl.Purifier.Internal;

namespace Stl.Purifier
{
    public interface IFunction : IAsyncDisposable
    {
        Task<IComputed?> InvokeAsync(object input, 
            IComputed? usedBy = null,
            ComputeContext? context = null,
            CancellationToken cancellationToken = default);
        IComputed? TryGetCached(object input, 
            IComputed? usedBy = null);
    }

    public interface IFunction<in TIn> : IFunction
        where TIn : notnull
    {
        Task<IComputed?> InvokeAsync(TIn input,
            IComputed? usedBy = null,
            ComputeContext? context = null,
            CancellationToken cancellationToken = default);
        IComputed? TryGetCached(TIn input, 
            IComputed? usedBy = null);
    }
    
    public interface IFunction<in TIn, TOut> : IFunction<TIn>
        where TIn : notnull
    {
        new Task<IComputed<TOut>?> InvokeAsync(TIn input,
            IComputed? usedBy = null,
            ComputeContext? context = null,
            CancellationToken cancellationToken = default);
        Task<TOut> InvokeAndStripAsync(TIn input,
            IComputed? usedBy = null,
            ComputeContext? context = null,
            CancellationToken cancellationToken = default);
        new IComputed<TOut>? TryGetCached(TIn input, 
            IComputed? usedBy = null);
    }

    public abstract class FunctionBase<TIn, TOut> : AsyncDisposableBase,
        IFunction<TIn, TOut>
        where TIn : notnull
    {
        protected Action<IComputed, object?> OnInvalidateHandler { get; set; }
        protected IComputedRegistry<(IFunction, TIn)> ComputedRegistry { get; }
        protected IRetryComputePolicy RetryComputePolicy { get; }
        protected IAsyncLockSet<(IFunction, TIn)> Locks { get; }
        protected object Lock => Locks;

        public FunctionBase(
            IComputedRegistry<(IFunction, TIn)> computedRegistry,
            IRetryComputePolicy? retryComputePolicy = null,
            IAsyncLockSet<(IFunction, TIn)>? locks = null)
        {
            retryComputePolicy ??= Purifier.RetryComputePolicy.Default;
            locks ??= new AsyncLockSet<(IFunction, TIn)>(ReentryMode.CheckedFail);
            ComputedRegistry = computedRegistry;
            RetryComputePolicy = retryComputePolicy; 
            Locks = locks;
            OnInvalidateHandler = (c, _) => Unregister((IComputed<TIn, TOut>) c);
        }

        async Task<IComputed?> IFunction.InvokeAsync(object input, 
            IComputed? usedBy,
            ComputeContext? context,
            CancellationToken cancellationToken) 
            => await InvokeAsync((TIn) input, usedBy, context, cancellationToken).ConfigureAwait(false);

        async Task<IComputed?> IFunction<TIn>.InvokeAsync(TIn input, 
            IComputed? usedBy, 
            ComputeContext? context,
            CancellationToken cancellationToken) 
            => await InvokeAsync(input, usedBy, context, cancellationToken).ConfigureAwait(false);

        public async Task<IComputed<TOut>?> InvokeAsync(TIn input, 
            IComputed? usedBy = null,
            ComputeContext? context = null,
            CancellationToken cancellationToken = default)
        {
            using var contextUseScope = context.Use();
            context = contextUseScope.Context;

            // Read-Lock-RetryRead-Compute-Store pattern

            var result = TryGetCached(input, usedBy);
            context.TryCaptureValue(result);
            if (result != null || (context.Options & ComputeOptions.TryGetCached) != 0) {
                if ((context.Options & ComputeOptions.Invalidate) == ComputeOptions.Invalidate)
                    result?.Invalidate();
                return result!;
            }

            using var @lock = await Locks.LockAsync((this, input), cancellationToken).ConfigureAwait(false);
            
            result = TryGetCached(input, usedBy);
            context.TryCaptureValue(result);
            if (result != null || (context.Options & ComputeOptions.TryGetCached) != 0) {
                if ((context.Options & ComputeOptions.Invalidate) == ComputeOptions.Invalidate)
                    result?.Invalidate();
                return result!;
            }

            for (var tryIndex = 0;; tryIndex++) {
                result = await ComputeAsync(input, cancellationToken).ConfigureAwait(false);
                if (result.IsValid)
                    break;
                if (!RetryComputePolicy.MustRetry(result, tryIndex))
                    break;
            }
            context.TryCaptureValue(result);
            result.Invalidated += OnInvalidateHandler;
            ((IComputedImpl?) usedBy)?.AddUsed((IComputedImpl) result);
            Register((IComputed<TIn, TOut>) result);
            return result;
        }

        public async Task<TOut> InvokeAndStripAsync(TIn input, 
            IComputed? usedBy = null,
            ComputeContext? context = null,
            CancellationToken cancellationToken = default)
        {
            using var contextUseScope = context.Use();
            context = contextUseScope.Context;

            // Read-Lock-RetryRead-Compute-Store pattern

            var result = TryGetCached(input, usedBy);
            context.TryCaptureValue(result);
            if (result != null || (context.Options & ComputeOptions.TryGetCached) != 0) {
                if ((context.Options & ComputeOptions.Invalidate) == ComputeOptions.Invalidate)
                    result?.Invalidate();
                return result.Strip();
            }

            using var @lock = await Locks.LockAsync((this, input), cancellationToken).ConfigureAwait(false);
            
            result = TryGetCached(input, usedBy);
            context.TryCaptureValue(result);
            if (result != null || (context.Options & ComputeOptions.TryGetCached) != 0) {
                if ((context.Options & ComputeOptions.Invalidate) == ComputeOptions.Invalidate)
                    result?.Invalidate();
                return result.Strip();
            }

            for (var tryIndex = 0;; tryIndex++) {
                result = await ComputeAsync(input, cancellationToken).ConfigureAwait(false);
                if (result.IsValid)
                    break;
                if (!RetryComputePolicy.MustRetry(result, tryIndex))
                    break;
            }
            context.TryCaptureValue(result);
            result.Invalidated += OnInvalidateHandler;
            ((IComputedImpl?) usedBy)?.AddUsed((IComputedImpl) result);
            Register((IComputed<TIn, TOut>) result);
            return result.Strip();
        }

        IComputed? IFunction.TryGetCached(object input, IComputed? usedBy) 
            => TryGetCached((TIn) input);
        IComputed? IFunction<TIn>.TryGetCached(TIn input, IComputed? usedBy) 
            => TryGetCached(input);
        public virtual IComputed<TOut>? TryGetCached(TIn input, IComputed? usedBy = null)
        {
            var value = ComputedRegistry.TryGet((this, input)) as IComputed<TIn, TOut>;
            if (value != null)
                ((IComputedImpl?) usedBy)?.AddUsed((IComputedImpl) value);
            return value;
        }

        // Protected & private

        protected abstract ValueTask<IComputed<TOut>> ComputeAsync(
            TIn input, CancellationToken cancellationToken);

        protected void Register(IComputed<TIn, TOut> computed) 
            => ComputedRegistry.Store((this, computed.Input), computed);

        protected void Unregister(IComputed<TIn, TOut> computed) 
            => ComputedRegistry.Remove((this, computed.Input), computed);
    }
}
