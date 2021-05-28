using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
//using Stl.Fusion.Server.Controllers;
using Stl.Fusion.Server.Internal;
using Stl.Reflection;
using Stl.Serialization;

namespace Stl.Fusion.Server
{
    public readonly struct FusionWebServerBuilder
    {
        private class AddedTag { }
        private static readonly ServiceDescriptor AddedTagDescriptor =
            new(typeof(AddedTag), new AddedTag());

        public FusionBuilder Fusion { get; }
        public IServiceCollection Services => Fusion.Services;

        internal FusionWebServerBuilder(FusionBuilder fusion,
            Action<IServiceProvider, WebSocketServer.Options>? webSocketServerOptionsBuilder)
        {
            Fusion = fusion;
            if (Services.Contains(AddedTagDescriptor))
                return;
            // We want above Contains call to run in O(1), so...
            Services.Insert(0, AddedTagDescriptor);

            Fusion.AddPublisher();
            Services.TryAddSingleton(c => {
                var options = new WebSocketServer.Options();
                webSocketServerOptionsBuilder?.Invoke(c, options);
                return options;
            });
            Services.TryAddSingleton<WebSocketServer>();

            // TODO: configure ParseRefModelBinderProvider/PageRefModelBinderProvider 
            // TODO: configure NewtonsoftJson with JsonNetSerializer.DefaultSettings
        }

        //TODO: add AddControllers and AddControllerFilter;
    }
}
