using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Stl.Extensibility;
using Stl.Plugins;

namespace Stl.Bootstrap.Web
{
    public static class WebHostBuilderEx
    {
        public static IWebHostBuilder UsePlugins<TPlugin>(
            this IWebHostBuilder builder,
            IEnumerable<TPlugin> plugins)
            where TPlugin : IWebHostPlugin
            => new WebHostPluginInvocation() {
                Tail = plugins.Cast<IWebHostPlugin>().Reverse().ToArray(),
                Handler = (plugin, invocation1) => plugin.Use(invocation1),
                Builder = builder,
            }.Invoke().Builder;

        public static IWebHostBuilder UsePlugins<TPlugin>(
            this IWebHostBuilder builder,
            IServiceProvider pluginHost)
            where TPlugin : IWebHostPlugin
            => builder.UsePlugins(pluginHost.GetPlugins<TPlugin>());
    }
}
