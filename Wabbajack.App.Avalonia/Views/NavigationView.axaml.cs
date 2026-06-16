using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.Messages;

namespace Wabbajack;

public partial class NavigationView : ReactiveUserControl<NavigationVM>
{
    private Dictionary<Button, HashSet<ScreenType>> _buttonScreens;

    public NavigationView()
    {
        AvaloniaXamlLoader.Load(this);

        // Map each nav button to the screens it represents, so the active one is highlighted
        // (mirrors the WPF NavigationView's ActiveScreen -> button-style logic).
        _buttonScreens = new()
        {
            { this.FindControl<Button>("HomeButton")!, new() { ScreenType.Home } },
            { this.FindControl<Button>("BrowseButton")!, new() { ScreenType.ModListGallery, ScreenType.Installer } },
            { this.FindControl<Button>("CompileButton")!, new() { ScreenType.CompilerHome, ScreenType.CompilerMain } },
            { this.FindControl<Button>("SettingsButton")!, new() { ScreenType.Settings } },
        };

        SetActive(ScreenType.Home);
        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(m => SetActive(m.Screen));
    }

    private void SetActive(ScreenType active)
    {
        foreach (var (button, screens) in _buttonScreens)
            button.Classes.Set("active", screens.Contains(active));
    }
}
