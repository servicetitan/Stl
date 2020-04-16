using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Stl.Caching;
using Stl.Plugins.Metadata;

namespace Stl.Plugins.Services 
{
    public abstract class CachingPluginFinderBase : IPluginFinder
    {
        private readonly Lazy<IAsyncCache<string, string>> _lazyCache;
        protected ILogger Logger { get; }

        public IAsyncCache<string, string> Cache => _lazyCache.Value;

        protected CachingPluginFinderBase(ILogger? logger = null)
        {
            Logger = logger ?? NullLogger.Instance;
            _lazyCache = new Lazy<IAsyncCache<string, string>>(CreateCache);
        }

        public PluginSetInfo FindPlugins() 
            => Task.Run(GetPluginSetInfoAsync).Result;

        // This method is async solely because ICache API is async
        protected virtual async Task<PluginSetInfo> GetPluginSetInfoAsync()
        {
            var cacheKey = GetCacheKey();
            if (cacheKey == null) {
                // Caching is off
                Logger.LogDebug("Plugin cache is disabled (cache key is null).");
                return await CreatePluginSetInfoAsync();
            }
            PluginSetInfo pluginSetInfo;
            var result = await Cache.TryGetAsync(cacheKey).ConfigureAwait(false);
            if (result.IsSome(out var v)) {
                Logger.LogDebug("Cached plugin set info found.");
                try {
                    pluginSetInfo = Deserialize(v);
                    return pluginSetInfo;
                }
                catch (Exception e) {
                    Logger.LogError(e, "Couldn't deserialize cached plugin set info.");
                }
            }
            Logger.LogDebug("Cached plugin set info is not available; populating...");
            pluginSetInfo = await CreatePluginSetInfoAsync();
            await Cache.SetAsync(cacheKey, Serialize(pluginSetInfo)).ConfigureAwait(false);
            Logger.LogDebug("Plugin set info is populated and cached.");
            return pluginSetInfo;
        }

        protected virtual JsonSerializerSettings GetJsonSerializerSettings() 
            => new JsonSerializerSettings() {
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            };

        protected virtual string Serialize(PluginSetInfo source) 
            => JsonConvert.SerializeObject(source, GetJsonSerializerSettings());

        protected virtual PluginSetInfo Deserialize(string source) 
            => JsonConvert.DeserializeObject<PluginSetInfo>(source, GetJsonSerializerSettings())
                ?? PluginSetInfo.Empty;

        protected abstract IAsyncCache<string, string> CreateCache();
        protected abstract string? GetCacheKey();
        protected abstract Task<PluginSetInfo> CreatePluginSetInfoAsync();
    }
}
