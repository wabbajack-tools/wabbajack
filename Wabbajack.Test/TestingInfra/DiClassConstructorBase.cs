using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TUnit.Core;
using TUnit.Core.Interfaces;

namespace Wabbajack.Test.TestingInfra;

/// <summary>
/// Base TUnit <see cref="IClassConstructor"/> that resolves a test class from a
/// Microsoft.Extensions.DependencyInjection container, preserving the constructor-injection model
/// the suite used under Xunit.DependencyInjection. Each concrete subclass supplies the service
/// configuration for one namespace (the equivalent of that namespace's old <c>Startup.cs</c>).
///
/// The provider is built once per subclass and cached; every test gets a fresh DI scope that is
/// disposed when the test ends (via <see cref="ITestEndEventReceiver"/>), matching the per-test
/// scoping xUnit gave us.
/// </summary>
public abstract class DiClassConstructorBase : IClassConstructor, ITestEndEventReceiver
{
    private static readonly ConcurrentDictionary<Type, IServiceProvider> Providers = new();

    private AsyncServiceScope _scope;
    private bool _hasScope;

    public Task<object> Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type,
        ClassConstructorMetadata classConstructorMetadata)
    {
        var provider = Providers.GetOrAdd(GetType(), _ =>
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(new TUnitLoggerProvider()));
            ConfigureServices(services);
            return services.BuildServiceProvider();
        });

        _scope = provider.CreateAsyncScope();
        _hasScope = true;

        var instance = ActivatorUtilities.GetServiceOrCreateInstance(_scope.ServiceProvider, type);
        return Task.FromResult(instance);
    }

    public ValueTask OnTestEnd(TestContext testContext) =>
        _hasScope ? _scope.DisposeAsync() : ValueTask.CompletedTask;

    public int Order => 0;

    /// <summary>Register the services this namespace's tests need (the old Startup body).</summary>
    protected abstract void ConfigureServices(IServiceCollection services);
}
