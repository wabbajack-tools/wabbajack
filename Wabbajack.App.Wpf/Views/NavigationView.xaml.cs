using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Wabbajack.Common;
using Wabbajack.Messages;

namespace Wabbajack;

/// <summary>
/// Interaction logic for NavigationView.xaml
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
        var activeButtonStyle = (Style)Application.Current.Resources["ActiveNavButtonStyle"];
        var mainButtonStyle = (Style)Application.Current.Resources["MainNavButtonStyle"];
        foreach(var (button, screens) in ButtonScreensDictionary)
        {
            if (screens.Contains(activeScreen))
                button.Style = activeButtonStyle;
            else
                button.Style = mainButtonStyle;
        }
    }
}
