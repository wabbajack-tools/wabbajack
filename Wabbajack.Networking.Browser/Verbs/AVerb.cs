using System.CommandLine;
using Avalonia.Threading;
using CefNet.Avalonia;
using ReactiveUI;
using Wabbajack.Networking.Browser;

namespace Wabbajack.CLI.Verbs;

public abstract class AVerb : IVerb
{
    public abstract Command MakeCommand();

    public string Instructions
    {
        set
        {
            Dispatcher.UIThread.Post(() =>
            {

                Program.MainWindowVM.Instructions = value;
            });
        }
    }

    public WebView Browser => Program.MainWindow.Browser;
}