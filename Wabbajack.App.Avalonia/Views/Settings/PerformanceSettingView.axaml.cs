using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.Views.Settings;

/// <summary>
/// One resource's limit. As in WPF, whatever is typed is reduced to its digits, and a box left empty means
/// the processor count.
/// </summary>
public partial class PerformanceSettingView : ReactiveUserControl<PerformanceSettingVM>
{
    public PerformanceSettingView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            MaxTasksTextBox.Text = ViewModel!.MaxTasks.ToString();

            this.WhenAnyValue(v => v.MaxTasksTextBox.Text)
                .Skip(1)
                .Select(str =>
                {
                    var digits = Regex.Replace(str ?? "", @"[^\d]+", "");
                    return !string.IsNullOrEmpty(digits) && long.TryParse(digits, out var value)
                        ? value
                        : Environment.ProcessorCount;
                })
                .Subscribe(value => ViewModel!.MaxTasks = value)
                .DisposeWith(disposables);
        });
    }
}
