using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class FileUploadSettingsView : ReactiveUserControl<SettingsVM>
{
    public FileUploadSettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
