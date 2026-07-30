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
        // WPF assigned Button.Style per instance; Avalonia has no settable Style property, so the
        // active/inactive look is expressed as style classes (see Themes/Controls.axaml, selectors
        // Button.ActiveNavButton / Button.MainNavButton).
        foreach (var (button, screens) in ButtonScreensDictionary)
        {
            var isActive = screens.Contains(activeScreen);
            button.Classes.Set("ActiveNavButton", isActive);
            button.Classes.Set("MainNavButton", !isActive);
        }
    }
}
