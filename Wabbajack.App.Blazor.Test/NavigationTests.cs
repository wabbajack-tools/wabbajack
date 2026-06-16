using System;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI;
using Wabbajack;
using Wabbajack.Blazor.Components;
using Wabbajack.Messages;

namespace Wabbajack.Blazor.Test;

// Behavior parity with the old Avalonia NavigationView tests: the sidebar renders the nav buttons and
// each fires the correct global navigation message.
[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class NavigationTests
{
    [Test]
    public async Task NavSidebar_RendersNavButtons()
    {
        using var ctx = new BunitContext();
        var vm = new NavigationVM(NullLogger<NavigationVM>.Instance);
        var cut = ctx.Render<NavSidebar>(ps => ps.Add(p => p.Vm, vm));

        await Assert.That(cut.FindAll("button.nav-btn").Count).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task NavSidebar_Browse_NavigatesToGallery()
    {
        using var ctx = new BunitContext();
        var vm = new NavigationVM(NullLogger<NavigationVM>.Instance);

        ScreenType? navigated = null;
        using var sub = MessageBus.Current.Listen<NavigateToGlobal>().Subscribe(m => navigated = m.Screen);

        var cut = ctx.Render<NavSidebar>(ps => ps.Add(p => p.Vm, vm));
        cut.FindAll("button.nav-btn").First(b => b.TextContent.Contains("Browse")).Click();

        await Assert.That(navigated).IsEqualTo(ScreenType.ModListGallery);
    }

    [Test]
    public async Task NavSidebar_Settings_NavigatesToSettings()
    {
        using var ctx = new BunitContext();
        var vm = new NavigationVM(NullLogger<NavigationVM>.Instance);

        ScreenType? navigated = null;
        using var sub = MessageBus.Current.Listen<NavigateToGlobal>().Subscribe(m => navigated = m.Screen);

        var cut = ctx.Render<NavSidebar>(ps => ps.Add(p => p.Vm, vm));
        cut.FindAll("button.nav-btn").First(b => b.TextContent.Contains("Settings")).Click();

        await Assert.That(navigated).IsEqualTo(ScreenType.Settings);
    }
}
