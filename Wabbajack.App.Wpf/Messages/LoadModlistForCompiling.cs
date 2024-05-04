using ReactiveUI;
using Wabbajack.Compiler;

namespace Wabbajack.Messages;

public class LoadModlistForCompiling
{
    public CompilerSettings CompilerSettings { get; set; }
    public LoadModlistForCompiling(CompilerSettings cs)
    {
        CompilerSettings = cs;
    }

    public static void Send(CompilerSettings cs)
    {
        MessageBus.Current.SendMessage(new LoadModlistForCompiling(cs));
    }
}