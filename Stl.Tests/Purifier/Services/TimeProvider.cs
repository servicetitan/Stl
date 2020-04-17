using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stl.Async;
using Stl.Purifier;
using Stl.Purifier.Autofac;
using Stl.Time;

namespace Stl.Tests.Purifier.Services
{
    public interface ITimeProvider
    {
        Moment GetTime();
        ValueTask<Moment> GetTimeAsync();
        ValueTask<Moment> GetTimerAsync(TimeSpan offset);
    }

    public class TimeProvider : ITimeProvider
    {
        protected ILogger Log { get; }

        public TimeProvider(ILogger<TimeProvider>? log = null) 
            => Log = log as ILogger ?? NullLogger.Instance;

        public Moment GetTime()
        {
            var now = RealTimeClock.Now;
            Log.LogDebug($"GetTime() -> {now}");
            return now;
        }

        public virtual ValueTask<Moment> GetTimeAsync()
        {
            var computed = Computed.Current();
            Task.Run(async () => {
                await Task.Delay(250).ConfigureAwait(false);
                computed!.Invalidate(
                    "Sorry, you were programmed to live for just 250ms :( " +
                    "Hopefully you enjoyed it.");
            });
            return ValueTaskEx.FromResult(GetTime());
        }

        public virtual async ValueTask<Moment> GetTimerAsync(TimeSpan offset)
        {
            var now = await GetTimeAsync().ConfigureAwait(false);
            return now + offset;
        }
    }
}