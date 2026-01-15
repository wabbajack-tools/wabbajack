using System;
using ReactiveUI;

namespace Wabbajack.Messages;

public class LoadModlistFromProtocol
{
    public string MachineUrl { get; }

    public LoadModlistFromProtocol(string machineUrl)
    {
        MachineUrl = machineUrl;
    }

    private static readonly object _lock = new();
    private static string? _pendingMachineUrl;

    public static void SetPending(string machineUrl)
    {
        if (string.IsNullOrWhiteSpace(machineUrl)) return;
        lock (_lock)
        {
            _pendingMachineUrl = machineUrl;
        }
    }

    public static bool TryConsumePending(out string machineUrl)
    {
        lock (_lock)
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

    public static void ClearPending()
    {
        lock (_lock)
        {
            _pendingMachineUrl = null;
        }
    }
}