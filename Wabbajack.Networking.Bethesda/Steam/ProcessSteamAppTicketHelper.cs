using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     Runs the <c>steam-app-ticket</c> verb in a child <c>wabbajack-cli</c> and collects what it printed.
/// </summary>
public class ProcessSteamAppTicketHelper : ISteamAppTicketHelper
{
    private readonly ILogger<ProcessSteamAppTicketHelper> _logger;

    public ProcessSteamAppTicketHelper(ILogger<ProcessSteamAppTicketHelper> logger)
    {
        _logger = logger;
    }

    public async Task<SteamAppTicketHelperResult> Run(IReadOnlyList<string> arguments, CancellationToken token)
    {
        var command = SteamAppTicketHelperLocator.Locate(AppContext.BaseDirectory,
                          Environment.ProcessPath, File.Exists,
                          Environment.GetEnvironmentVariable(SteamAppTicketHelperLocator.OverrideVariable))
                      ?? throw new SteamAppTicketException(SteamAppTicketError.HelperFailed,
                          $"{SteamAppTicketHelperLocator.ExecutableName} is not next to this build, so there is " +
                          "nothing to ask Steam with. A Wabbajack install carries it in its cli folder.");

        var info = new ProcessStartInfo
        {
            FileName = command.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,

            // The helper's own folder, not the game's. The library is loaded by absolute path, which is
            // enough for the loader to find anything it sits beside, and pointing the child at the game
            // folder instead would have the CLI write its log folder into the user's game install.
            WorkingDirectory = Path.GetDirectoryName(command.LeadingArguments.Count > 0
                ? command.LeadingArguments[0]
                : command.FileName) ?? AppContext.BaseDirectory
        };

        foreach (var argument in command.WithArguments(arguments)) info.ArgumentList.Add(argument);

        _logger.LogDebug("Running {File} {Arguments}", info.FileName, string.Join(' ', info.ArgumentList));

        using var process = new Process {StartInfo = info};

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new SteamAppTicketException(SteamAppTicketError.HelperFailed,
                $"{command.FileName} could not be started: {ex.Message}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        return new SteamAppTicketHelperResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch (Exception ex)
        {
            // A child that has already gone, or that cannot be killed, changes nothing about the answer the
            // caller is about to get, which is that there is no ticket.
            _logger.LogDebug(ex, "Could not end the ticket helper");
        }
    }
}
