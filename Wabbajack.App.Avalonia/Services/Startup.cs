using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
///     One Wabbajack at a time, as in WPF: the first process owns a named mutex, and a later one hands its
///     arguments over the protocol pipe and exits. The names are the WPF app's, so the two never run together.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private bool _owned;

    public SingleInstance()
    {
        _mutex = new Mutex(true, "Wabbajack-{F8C1E8F0-3E3A-4B3D-9F4A-1E5C6D7E8F9A}", out _owned);
    }

    public bool IsFirstInstance => _owned;

    public void Dispose()
    {
        if (_owned)
        {
            _mutex.ReleaseMutex();
            _owned = false;
        }
        _mutex.Dispose();
    }
}

/// <summary>The pipe a second launch sends its arguments down, joined with '|', as WPF's did.</summary>
public static class ProtocolPipe
{
    private const string Name = "WabbajackProtocolPipe";

    public static void Send(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Name, PipeDirection.Out);
            client.Connect(1000);
            using var writer = new StreamWriter(client);
            writer.WriteLine(string.Join("|", args));
            writer.Flush();
        }
        catch (Exception ex)
        {
            // The running instance did not answer; there is nothing more a second launch can do.
            Debug.WriteLine($"Failed to send args to running instance: {ex.Message}");
        }
    }

    /// <summary>Listens until cancelled, handing each message's arguments to <paramref name="received" />.</summary>
    public static void Listen(Action<string[]> received, ILogger logger, CancellationToken token)
    {
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(Name, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server);
                    var line = await reader.ReadLineAsync(token);
                    if (!string.IsNullOrWhiteSpace(line))
                        received(line.Split('|'));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error in protocol pipe server");
                    await Task.Delay(100, CancellationToken.None);
                }
            }
        }, CancellationToken.None);
    }
}

/// <summary>The WPF app's checks before anything else starts, and its way out of unreadable settings.</summary>
public static class StartupChecks
{
    public const string ProtocolPrefix = "wabbajack://";

    /// <summary>The payload of a wabbajack:// link, unescaped, or null when the argument is not one.</summary>
    public static string? ProtocolPayload(string arg) =>
        arg.StartsWith(ProtocolPrefix, StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(arg[ProtocolPrefix.Length..]).Trim()
            : null;

    /// <summary>
    ///     The .wabbajack file an argument names, or null. WPF parsed any single argument as a path to find out,
    ///     which threw for anything that is not one, so a lone "--version" took the process down.
    /// </summary>
    public static AbsolutePath? WabbajackFile(string arg)
    {
        try
        {
            var path = arg.ToAbsolutePath();
            return path.FileExists() && path.Extension == Ext.Wabbajack ? path : null;
        }
        catch (PathException)
        {
            return null;
        }
    }

    public static bool IsAdmin()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var identity = WindowsIdentity.GetCurrent();
            if (identity.Owner is { } owner) return owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Refuses to run from inside the Windows folder, and runs from the executable's folder whatever the working
    ///     directory was: a shortcut or a file association can start it in System32, and relative paths then land
    ///     there. Returns false when the app should exit.
    /// </summary>
    public static bool EnsureSafeWorkingDirectory()
    {
        if (!OperatingSystem.IsWindows()) return true;

        var exePath = Environment.ProcessPath ?? throw new Exception("Process location is unavailable!");
        var exeDir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrWhiteSpace(exeDir))
            throw new Exception("Executable directory is unavailable!");

        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (IsUnderDirectory(exeDir, windowsDir))
        {
            NativeMessageBox.ShowError(
                "Wabbajack refuses to run from Windows system folders (Windows/System32) please use a dedicated Wabbajack folder ( and NOT a folder like Downloads, Desktop etc ).",
                "Unsafe launch location");
            return false;
        }

        try
        {
            if (!string.Equals(Path.GetFullPath(Directory.GetCurrentDirectory()), Path.GetFullPath(exeDir),
                    StringComparison.OrdinalIgnoreCase))
                Directory.SetCurrentDirectory(exeDir);
        }
        catch (Exception ex)
        {
            NativeMessageBox.ShowError("Wabbajack failed to set a safe working directory. It will now exit.\n\n" + ex,
                "Failed to set working directory");
            return false;
        }

        return true;
    }

    private static bool IsUnderDirectory(string childPath, string parentPath)
    {
        childPath = Path.GetFullPath(childPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
        parentPath = Path.GetFullPath(parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
        return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Arguments that are neither a wabbajack:// link nor a single .wabbajack file are a command line. WPF ran
    ///     those in-process through the CLI project; that project cannot be referenced from here, so they go to the
    ///     wabbajack-cli.exe every install ships in its cli folder, and its exit code comes back.
    /// </summary>
    public static int RunCli(string[] args)
    {
        var cli = Path.Combine(AppContext.BaseDirectory, "cli", "wabbajack-cli.exe");
        if (!File.Exists(cli))
        {
            NativeMessageBox.ShowError($"Command line arguments need the Wabbajack CLI, which was not found at {cli}.",
                "Wabbajack CLI not found");
            return 1;
        }

        var info = new ProcessStartInfo(cli) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(cli)! };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>
    ///     What WPF offered when the settings under %localappdata%\Wabbajack could not be opened: turn the deny
    ///     rules on that folder into full control, then restart through the CLI's restart verb.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void RepairSettingsFolderAndRestart(AbsolutePath folder)
    {
        try
        {
            var dir = new DirectoryInfo(folder.ToString());
            if (dir.Exists)
            {
                var security = dir.GetAccessControl();
                foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(NTAccount)))
                {
                    if (rule.AccessControlType != AccessControlType.Deny) continue;
                    security.RemoveAccessRule(rule);
                    security.AddAccessRule(new FileSystemAccessRule(rule.IdentityReference, FileSystemRights.FullControl,
                        InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None,
                        AccessControlType.Allow));
                    dir.SetAccessControl(security);
                }
            }
        }
        catch (Exception)
        {
            // Nothing more to try; the restart below will show the same failure if it persists.
        }

        try
        {
            // WPF named wabbajack-cli.exe without its folder, so the restart only worked when the working
            // directory happened to be the cli folder. The cli folder is where it lives.
            var cliDir = Path.Combine(AppContext.BaseDirectory, "cli");
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(cliDir, "wabbajack-cli.exe"),
                Arguments = "restart",
                WorkingDirectory = cliDir,
                CreateNoWindow = true
            });
        }
        catch (Exception)
        {
        }
    }
}
