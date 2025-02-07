using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using ReactiveUI;
using System.Linq;
using System;

namespace Wabbajack;

public partial class PerformanceSettingView : ReactiveUserControl<PerformanceSettingVM>
{
    public PerformanceSettingView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(v => v.MaxTasksTextBox.Text)
                .Select(str => {
                    var numericString = Regex.Replace(str, @"[^\d]+", "");
                    if (!string.IsNullOrEmpty(numericString))
                        return long.Parse(numericString);
                    else return Environment.ProcessorCount;
                })
                .BindToStrict(this, x => x.ViewModel.MaxTasks)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.HumanName)
                .BindToStrict(this, v => v.HumanNameTextBlock.Text)
                .DisposeWith(disposables);

            MaxTasksTextBox.Text = ViewModel.MaxTasks.ToString();
        });
    }
}
