using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using Wabbajack.Common;
using Wabbajack.Messages;

namespace Wabbajack;

/// <summary>
/// Interaction logic for NavigationView.axaml
/// </summary>
public partial class NavigationView : ReactiveUserControl<NavigationVM>
{
    public Dictionary<Button, HashSet<ScreenType>> ButtonScreensDictionary { get; set; }
    public NavigationView()
    {
        InitializeComponent();
        ButtonScreensDictionary = new() {
            { HomeButton, [ScreenType.Home] },
            { BrowseButton, [ScreenType.ModListGallery, ScreenType.Installer] },
            { CompileButton, [ScreenType.CompilerHome, ScreenType.CompilerMain] },
            { SettingsButton, [ScreenType.Settings] },
        };
        this.WhenActivated(dispose =>
        {
            this.BindCommand(ViewModel, vm => vm.BrowseCommand, v => v.BrowseButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.HomeCommand, v => v.HomeButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.CompileModListCommand, v => v.CompileButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.SettingsCommand, v => v.SettingsButton)
                .DisposeWith(dispose);

            this.WhenAny(x => x.ViewModel.Version)
                .Select(version => $"v{version}")
                .BindToStrict(this, v => v.VersionTextBlock.Text)
                .DisposeWith(dispose);


            this.WhenAny(x => x.ViewModel.ActiveScreen)
                .Subscribe(x => SetButtonActive(x))
                .DisposeWith(dispose);
        });
    }

    private void SetButtonActive(ScreenType activeScreen)
    {
        // TODO: WPF's per-instance Button.Style assignment has no direct Avalonia equivalent
        // (Avalonia controls don't expose a settable Style property). Translated to adding the
        // looked-up resource Style to each button's local Styles collection, which applies it
        // scoped to that control. Verify the Avalonia theme defines "ActiveNavButtonStyle" and
        // "MainNavButtonStyle" as Avalonia Style objects (with a Selector, e.g. Selector="Button")
        // rather than WPF's TargetType-based Style, since Avalonia styles are selector-based.
        var activeButtonStyle = (Style)Application.Current!.Resources["ActiveNavButtonStyle"]!;
        var mainButtonStyle = (Style)Application.Current!.Resources["MainNavButtonStyle"]!;
        foreach (var (button, screens) in ButtonScreensDictionary)
        {
            button.Styles.Clear();
            button.Styles.Add(screens.Contains(activeScreen) ? activeButtonStyle : mainButtonStyle);
        }
    }
}
