using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Mixins;
using DynamicData;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.App.Utilities;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Controls;

public class LogViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly LoggerProvider _provider;

    private readonly SourceCache<LoggerProvider.ILogMessage, long> _messages;

    public readonly ReadOnlyObservableCollection<LoggerProvider.ILogMessage> _messagesFiltered;
    public ReadOnlyObservableCollection<LoggerProvider.ILogMessage> Messages => _messagesFiltered;

    public LogViewModel(LoggerProvider provider)
    {
        _messages = new SourceCache<LoggerProvider.ILogMessage, long>(m => m.MessageId);
        _messages.LimitSizeTo(100);
        
        Activator = new ViewModelActivator();
        _provider = provider;
        
        _messages.Connect()
            .Bind(out _messagesFiltered)
            .Subscribe();
        
        _provider.Messages
            .Subscribe(m => _messages.AddOrUpdate(m));
    }
    
}