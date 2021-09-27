using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Views
{
    public partial class SettingsView : ScreenBase<SettingsViewModel>
    {
        public SettingsView()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
  
            });
        }

    }
}