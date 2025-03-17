using ReactiveUI;
using Wabbajack.Compiler;

namespace Wabbajack.Messages;

public class LoadCompilerSettings
{
    public CompilerSettings CompilerSettings { get; set; }
    public LoadCompilerSettings(CompilerSettings cs)
    {
        CompilerSettings = cs;
    }

    public static void Send(CompilerSettings cs)
    {
        MessageBus.Current.SendMessage(new LoadCompilerSettings(cs));
    }
}