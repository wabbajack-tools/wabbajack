using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Downloaders.GameFile;

/// <summary>
///     Where each game file source sits in the order they are asked, in one place because the order is a
///     decision about the sources together rather than about any one of them. Gaps are left on purpose, the
///     way <c>PreflightCheckIds</c> leaves them.
///     <para>
///         Steam first: it carries the game's own files, the Creation Kit, and the handful of Creations
///         that were built into a depot. Bethesda second: everything Steam publishes is cheaper to fetch
///         from Steam - the user is already entitled to it and nothing has to talk to a live game client -
///         and the Bethesda path exists for the files no depot has.
///     </para>
/// </summary>
public static class GameFileRestorerOrder
{
    /// <summary>Steam's depots.</summary>
    public const int Steam = 100;

    /// <summary>Bethesda's Creations, fetched with a ticket from the running Steam client.</summary>
    public const int Bethesda = 200;
}

/// <summary>
///     The game file sources a host has registered, and their order.
///     <para>
///         This exists because <c>PreflightRunner</c> resolves a single <see cref="IGameFileRestorer" /> and
///         every source registers itself with <c>TryAdd</c>, so the first one in would otherwise be the only
///         one anybody ever sees. Collecting the types here and building one
///         <see cref="CompositeGameFileRestorer" /> out of them keeps that single resolve working while
///         letting two independent registration calls - neither of which knows the other exists - both be
///         reached.
///     </para>
///     <para>
///         Types rather than instances: registration runs while the collection is still being built, so
///         there is nothing to resolve from yet. They are resolved once, when the composite is first asked
///         for.
///     </para>
/// </summary>
public sealed class GameFileRestorerRegistry
{
    private readonly List<(int Order, Type Type)> _sources = new();

    /// <summary>
    ///     Adds a source, ignoring a type that is already in. A registration extension may be called twice
    ///     - by a host and by something the host depends on - and the second call must not put the same
    ///     source in the chain twice.
    /// </summary>
    public void Add(int order, Type type)
    {
        lock (_sources)
        {
            if (_sources.Any(s => s.Type == type)) return;
            _sources.Add((order, type));
        }
    }

    /// <summary>The registered source types, in the order they will be asked.</summary>
    public IReadOnlyList<Type> Types
    {
        get
        {
            lock (_sources)
            {
                return _sources.OrderBy(s => s.Order).Select(s => s.Type).ToArray();
            }
        }
    }

    /// <summary>
    ///     The restorer a host with these sources should use. One source is handed back as itself rather
    ///     than wrapped, so a host that registers only Steam behaves exactly as it did before there was
    ///     anything to compose - same <c>SourceName</c> in every message, same answers.
    /// </summary>
    public IGameFileRestorer Build(IServiceProvider provider)
    {
        var types = Types;
        if (types.Count == 0)
            throw new InvalidOperationException(
                "No game file restorer has been registered, so there is nothing to build.");

        var restorers = types.Select(t => (IGameFileRestorer) provider.GetRequiredService(t)).ToArray();
        if (restorers.Length == 1) return restorers[0];

        return new CompositeGameFileRestorer(
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<CompositeGameFileRestorer>(), restorers);
    }
}

public static class GameFileRestorerServiceExtensions
{
    /// <summary>
    ///     Registers <typeparamref name="TRestorer" /> as one of the sources behind the single
    ///     <see cref="IGameFileRestorer" /> the rest of the app resolves.
    ///     <para>
    ///         Every registration is a <c>TryAdd</c>, including the <see cref="IGameFileRestorer" /> itself,
    ///         so a host that wants to supply its own restorer outright still wins and a host that registers
    ///         none gets none - which is what <c>GameFileRepair</c> reads as "this build has no way to fetch
    ///         game files".
    ///     </para>
    /// </summary>
    /// <param name="order">See <see cref="GameFileRestorerOrder" />.</param>
    public static IServiceCollection AddGameFileRestorer<TRestorer>(this IServiceCollection services, int order)
        where TRestorer : class, IGameFileRestorer
    {
        services.TryAddSingleton<TRestorer>();

        Registry(services).Add(order, typeof(TRestorer));

        services.TryAddSingleton<IGameFileRestorer>(provider =>
            provider.GetRequiredService<GameFileRestorerRegistry>().Build(provider));

        return services;
    }

    /// <summary>
    ///     The one registry this collection uses, created on first ask. Found by looking for the instance
    ///     already in the collection rather than kept in a static, so two service collections in one process
    ///     - which is every test host in the suite - do not share a chain.
    /// </summary>
    private static GameFileRestorerRegistry Registry(IServiceCollection services)
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(GameFileRestorerRegistry))
            ?.ImplementationInstance as GameFileRestorerRegistry;
        if (existing != null) return existing;

        var registry = new GameFileRestorerRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
