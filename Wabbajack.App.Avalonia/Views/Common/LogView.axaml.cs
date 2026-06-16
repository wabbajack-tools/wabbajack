using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Wabbajack;

// Host this by setting DataContext to a Wabbajack.Models.LogStream instance.
// The control binds to LogStream.MessageLog (ReadOnlyObservableCollection<ILogMessage>)
// and renders each ILogMessage.ShortMessage.
public partial class LogView : UserControl
{
    public LogView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
