using System;
using System.Windows.Input;
using Avalonia.Media;

namespace Wabbajack.App.Avalonia.LoginManagers;

/// <summary>One tile on the Logins settings card: a site, whether this machine holds a login for it, and the button.</summary>
public interface INeedsLogin
{
    string SiteName { get; }
    ICommand TriggerLogin { get; }
    ICommand ClearLogin { get; }
    ICommand ToggleLogin { get; }

    /// <summary>The site's mark, or null for a glyph in its place.</summary>
    IImage? Icon { get; }

    Type LoginFor();
    bool LoggedIn { get; }
}
