using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stl.Channels;
using Stl.IO;
using Stl.Fusion.Autofac;
using Stl.Fusion;
using Stl.Fusion.Bridge;
using Stl.Fusion.Bridge.Messages;
using Stl.Security;
using Stl.Testing;
using Stl.Testing.Internal;
using Stl.Tests.Fusion.Model;
using Stl.Tests.Fusion.Services;
using Stl.Text;
using Xunit.Abstractions;
using Xunit.DependencyInjection.Logging;

namespace Stl.Tests.Fusion
{
    public class FusionTestOptions
    {
        public bool UseInMemoryDatabase { get; set; }
    }

    public class FusionTestBase : TestBase
    {
        public FusionTestOptions Options { get; }
        public bool IsLoggingEnabled { get; set; } = true;
        public IServiceProvider Services { get; }
        public ILifetimeScope Container { get; }
        public ILogger Log { get; }
        public TestDbContext DbContext => Container.Resolve<TestDbContext>();
        public IPublisher Publisher => Container.Resolve<IPublisher>();
        public IChannelHub<PublicationMessage> ChannelHub => Publisher.ChannelHub; // Publisher should be resolved first!

        public FusionTestBase(ITestOutputHelper @out, FusionTestOptions? options = null) : base(@out)
        {
            Options = options ?? new FusionTestOptions();
            Services = CreateServices();
            Container = Services.GetRequiredService<ILifetimeScope>();
            Log = (ILogger) Container.Resolve(typeof(ILogger<>).MakeGenericType(GetType()));
        }

        public virtual Task InitializeAsync() 
            => DbContext.Database.EnsureCreatedAsync();
        public virtual Task DisposeAsync() 
            => Task.CompletedTask.ContinueWith(_ => Container?.Dispose()); 

        protected virtual IServiceProvider CreateServices()
        {
            // IServiceCollection-based services
            var services = (IServiceCollection) new ServiceCollection();
            ConfigureServices(services);

            // Native Autofac services
            var builder = new AutofacServiceProviderFactory().CreateBuilder(services);
            ConfigureServices(builder);

            var container = builder.Build();
            return new AutofacServiceProvider(container);
        }

        protected virtual void ConfigureServices(IServiceCollection services)
        {

            services.AddSingleton(Out);

            // Logging
            services.AddLogging(logging => {
                var debugCategories = new HashSet<string> {
                    "Stl.Tests.Fusion",
                    // DbLoggerCategory.Database.Transaction.Name,
                    // DbLoggerCategory.Database.Connection.Name,
                    // DbLoggerCategory.Database.Command.Name,
                    // DbLoggerCategory.Query.Name,
                    // DbLoggerCategory.Update.Name,
                };

                bool LogFilter(string category, LogLevel level)
                    => IsLoggingEnabled && 
                        debugCategories.Any(category.StartsWith) 
                        && level >= LogLevel.Debug;

                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddFilter(LogFilter);
                logging.AddDebug();
                // XUnit logging requires weird setup b/c otherwise it filters out
                // everything below LogLevel.Information 
                logging.AddProvider(new XunitTestOutputLoggerProvider(
                    new TestOutputHelperAccessor(Out), 
                    LogFilter));
            });

            // DbContext & related services
            var testType = GetType();
            var appTempDir = PathEx.GetApplicationTempDirectory("", true);
            var dbPath = appTempDir & PathEx.GetHashedName($"{testType.Name}_{testType.Namespace}.db");
            if (File.Exists(dbPath))
                File.Delete(dbPath);

            if (Options.UseInMemoryDatabase)
                services
                    .AddEntityFrameworkInMemoryDatabase()
                    .AddDbContextPool<TestDbContext>(builder => {
                        builder.UseInMemoryDatabase(dbPath);
                    });
            else
                services
                    .AddEntityFrameworkSqlite()
                    .AddDbContextPool<TestDbContext>(builder => {
                        builder.UseSqlite($"Data Source={dbPath}", sqlite => { });
                    });

            services.AddSingleton<ITestDbContextPool, TestDbContextPool>();
        }

        protected virtual void ConfigureServices(ContainerBuilder builder)
        {
            var publicationIdGenerator = new TransformingGenerator<string, Symbol>(
                new RandomStringGenerator(), 
                s => new Symbol($"p-{s}"));

            builder.AddFusion();
            builder.AddFusionPublisher("publisher", publicationIdGenerator);

            // Computed providers
            builder.RegisterType<SimplestProvider>()
                .As<ISimplestProvider>().ComputedProvider();
            builder.RegisterType<TimeProvider>()
                .As<ITimeProvider>().ComputedProvider();
            builder.RegisterType<UserProvider>()
                .As<IUserProvider>().ComputedProvider();

            // Regular services 
            builder.RegisterType<UserProvider>()
                .As<UserProvider>().InstancePerLifetimeScope();
        }
        public virtual TestChannelPair<PublicationMessage> CreateChannelPair(
            string name, bool dump = true) 
            => new TestChannelPair<PublicationMessage>(name, dump ? Out : null);
    }
}
