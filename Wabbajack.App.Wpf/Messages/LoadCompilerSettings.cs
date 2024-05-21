using ReactiveUI;
using System.IO;
using Wabbajack.Compiler;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;

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