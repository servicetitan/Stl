using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Extras.DynamicProxy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stl.Concurrency;
using Stl.IO;
using Stl.Locking;
using Stl.Purifier;
using Stl.Purifier.Autofac;
using Stl.Reflection;
using Stl.Testing;
using Stl.Testing.Internal;
using Stl.Tests.Purifier.Model;
using Stl.Tests.Purifier.Services;
using Xunit.Abstractions;
using Xunit.DependencyInjection.Logging;

namespace Stl.Tests.Purifier
{
    public class PurifierTestOptions
    {
        public bool UseInMemoryDatabase { get; set; }
    }

    public class PurifierTestBase : TestBase
    {
        public PurifierTestOptions Options { get; }
        public bool IsLoggingEnabled { get; set; } = true;
        public IServiceProvider Services { get; }
        public ILifetimeScope Container { get; }
        public ILogger Log { get; }
        public TestDbContext DbContext => Container.Resolve<TestDbContext>();

        public PurifierTestBase(ITestOutputHelper @out, PurifierTestOptions? options = null) : base(@out)
        {
            Options = options ?? new PurifierTestOptions();
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
                    "Stl.Tests.Purifier",
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
                    new SimpleTestOutputHelperAccessor(Out), 
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
            // Computed/Function related
            builder.Register(c => new ConcurrentIdGenerator<long>(i => {
                    var id = i * 10000;
                    return () => ++id;
                })).SingleInstance();
            builder.RegisterGeneric(typeof(ComputedRegistry<>))
                .As(typeof(IComputedRegistry<>))
                .SingleInstance();
            builder.RegisterGeneric(typeof(AsyncLockSet<>))
                .As(typeof(IAsyncLockSet<>))
                .SingleInstance();
            builder.Register(c => ArgumentComparerProvider.Default)
                .SingleInstance();
            builder.RegisterType<ComputedInterceptor>()
                .SingleInstance();
            builder.RegisterType<CustomFunction>()
                .EnableClassInterceptors()
                .InterceptedBy(typeof(ComputedInterceptor))
                .SingleInstance();

            // Services
            builder.RegisterType<TimeProvider>()
                .As<ITimeProvider>()
                .EnableClassInterceptors()
                .InterceptedBy(typeof(ComputedInterceptor))
                .SingleInstance();
            builder.RegisterType<UserProvider>()
                .As<IUserProvider>()
                .EnableClassInterceptors()
                .InterceptedBy(typeof(ComputedInterceptor))
                .SingleInstance();
            builder.RegisterType<UserProvider>()
                .As<UserProvider>()
                .InstancePerLifetimeScope();
        }
    }
}