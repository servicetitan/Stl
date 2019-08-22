using Microsoft.AspNetCore.Hosting;
using Stl.Extensibility;

namespace Stl.Bootstrap.Web 
{
    public interface IWebHostPlugin
    {
        void Use(WebHostPluginInvocation invocation);
    }

    public class WebHostPluginInvocation : ChainInvocationBase<IWebHostPlugin, WebHostPluginInvocation>
    {
        public IWebHostBuilder Builder { get; set; } = default!;
    }
}
