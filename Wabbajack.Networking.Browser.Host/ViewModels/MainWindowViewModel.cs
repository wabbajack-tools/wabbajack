using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.CLI.Verbs;
using Wabbajack.Common;

namespace Wabbajack.Networking.Browser.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IEnumerable<IVerb> _verbs;

        public MainWindowViewModel(IEnumerable<IVerb> verbs)
        {
            _verbs = verbs;
            this.WhenActivated(disposables =>
            {
                ExecuteCommand().FireAndForget();
                Disposable.Empty.DisposeWith(disposables);
            });
        }

        [Reactive]
        public string Instructions { get; set; }

        private async Task ExecuteCommand()
        {
            while (Program.MainWindow.Browser == null)
                await Task.Delay(250);
            
            var root = new RootCommand();
            foreach (var verb in _verbs)
                root.Add(verb.MakeCommand());

            var code = await root.InvokeAsync(Program.Args);
            Environment.Exit(code);
        }
    }
}
