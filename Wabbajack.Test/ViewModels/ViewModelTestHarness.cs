using System;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace Wabbajack.ViewModels.Test;

/// <summary>
/// Helper for headless ViewModel tests: resolves ViewModels from the offline DI container and
/// activates them (firing their <c>WhenActivated</c> blocks) without a running WPF app.
/// </summary>
public sealed class ViewModelTestHarness
{
    private readonly IServiceProvider _provider;

    public ViewModelTestHarness(IServiceProvider provider) => _provider = provider;

    public T Resolve<T>() where T : notnull => ActivatorUtilities.GetServiceOrCreateInstance<T>(_provider);

    /// <summary>Fires the ViewModel's WhenActivated blocks; dispose the result to deactivate.</summary>
    public IDisposable Activate(IActivatableViewModel vm) => vm.Activator.Activate();
}
