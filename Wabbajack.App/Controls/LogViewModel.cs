using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls.Mixins;
using Avalonia.Input;
using DynamicData;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Utilities;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Controls;

public class LogViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly LoggerProvider _provider;
    public ReadOnlyObservableCollection<LoggerProvider.ILogMessage> Messages => _provider.MessageLog;
    
    [Reactive]
    public ReactiveCommand<Unit, Unit> CopyLogFile { get; set; } 

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
}