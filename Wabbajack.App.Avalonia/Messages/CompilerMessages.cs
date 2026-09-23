using ReactiveUI;
using Wabbajack.Compiler;

namespace Wabbajack.App.Avalonia.Messages;

/// <summary>Hands a modlist's compiler settings to the compiler screen, as the WPF app's message did.</summary>
public class LoadCompilerSettings(CompilerSettings compilerSettings)
{
    public CompilerSettings CompilerSettings { get; } = compilerSettings;

    public static void Send(CompilerSettings cs) => MessageBus.Current.SendMessage(new LoadCompilerSettings(cs));
}

/// <summary>Asks the Create a list screen to re-read its recently compiled lists.</summary>
public class ReloadCompiledModLists
{
    public static void Send() => MessageBus.Current.SendMessage(new ReloadCompiledModLists());
}
