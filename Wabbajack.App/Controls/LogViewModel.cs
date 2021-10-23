using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia;
using Avalonia.Input;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Utilities;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Controls;

public class LogViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly LoggerProvider _provider;

    public LogViewModel(LoggerProvider provider)
    {
        Activator = new ViewModelActivator();
        _provider = provider;

        CopyLogFile = ReactiveCommand.Create(() =>
        {
            var obj = new DataObject();
            obj.Set(DataFormats.FileNames, new List<string> {_provider.LogPath.ToString()});
            Application.Current.Clipboard.SetDataObjectAsync(obj);
        });
    }

    public ReadOnlyObservableCollection<LoggerProvider.ILogMessage> Messages => _provider.MessageLog;

    [Reactive] public ReactiveCommand<Unit, Unit> CopyLogFile { get; set; }
}