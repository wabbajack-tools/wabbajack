using ReactiveUI;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Messages;

/// <summary>Hands a downloaded .wabbajack file, and its gallery entry if it came from one, to the installer.</summary>
public class LoadModlistForInstalling(AbsolutePath path, ModlistMetadata? metadata)
{
    public AbsolutePath Path { get; } = path;
    public ModlistMetadata? Metadata { get; } = metadata;

    public static void Send(AbsolutePath path, ModlistMetadata? metadata) =>
        MessageBus.Current.SendMessage(new LoadModlistForInstalling(path, metadata));
}

/// <summary>
/// A wabbajack:// link to open. Also kept as pending until the gallery takes it, because a link can arrive
/// before the gallery has been shown.
/// </summary>
public class LoadModlistFromProtocol(string machineUrl)
{
    private static readonly object Lock = new();
    private static string? _pendingMachineUrl;

    public string MachineUrl { get; } = machineUrl;

    public static void SetPending(string machineUrl)
    {
        if (string.IsNullOrWhiteSpace(machineUrl)) return;
        lock (Lock) _pendingMachineUrl = machineUrl;
    }

    public static bool TryConsumePending(out string machineUrl)
    {
        lock (Lock)
        {
            if (string.IsNullOrWhiteSpace(_pendingMachineUrl))
            {
                machineUrl = "";
                return false;
            }

            machineUrl = _pendingMachineUrl!;
            _pendingMachineUrl = null;
            return true;
        }
    }

    public static void Send(string machineUrl)
    {
        SetPending(machineUrl);
        MessageBus.Current.SendMessage(new LoadModlistFromProtocol(machineUrl));
    }
}

/// <summary>Floating panes the main window can show, as the WPF app's FloatingScreenType.</summary>
public enum FloatingScreenType
{
    None,
    ModListDetails,
    FileUpload
}

public class ShowFloatingWindow(FloatingScreenType screen)
{
    public FloatingScreenType Screen { get; } = screen;

    public static void Send(FloatingScreenType screen) => MessageBus.Current.SendMessage(new ShowFloatingWindow(screen));
}
