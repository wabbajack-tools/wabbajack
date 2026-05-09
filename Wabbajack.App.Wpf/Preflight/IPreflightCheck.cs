// Wabbajack.App.Wpf/Preflight/IPreflightCheck.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace Wabbajack.Preflight;

public enum PreflightCheckStatus { Pending, Checking, Passed, Failed }

public interface IPreflightCheck : INotifyPropertyChanged, IDisposable
{
    string Title { get; }
    PreflightCheckStatus Status { get; }
    string? FailureMessage { get; }
    ICommand? ActionCommand { get; }
    string? ActionLabel { get; }
    IReadOnlyList<PreflightSubItem>? SubItems { get; }
}
