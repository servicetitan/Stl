using System;
using System.Collections.Generic;
using System.Composition;
using Microsoft.Extensions.Logging;
using Stl.Plugins;

namespace Stl.Tests.Plugins
{
    public abstract class TestPlugin : Plugin
    {
        protected override IEnumerable<InjectionPoint> AcquireInjectionPoints()
        {
            yield return new StartupInjectionPoint(() => Logger.LogInformation("Starting."));
            yield return new ConfigureServicesInjectionPoint(sc => Logger.LogInformation("Configuring services."));
        }
    }

    [Export(typeof(TestPlugin))]
    public class TestPlugin1 : TestPlugin
    {
        protected override IEnumerable<Type> AcquireDependencies() => new [] { typeof(TestPlugin2) };
    }
    
    [Export(typeof(TestPlugin))]
    public class TestPlugin2 : TestPlugin
    {
    }
}
