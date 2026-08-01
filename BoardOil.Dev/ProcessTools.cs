using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace BoardOil.Dev;

internal static class ExecutableLocator
{
    public static string? Find(string command)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        var candidateNames = GetCandidateNames(command);
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidateName in candidateNames)
            {
                var candidatePath = Path.Combine(directory.Trim('"'), candidateName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetCandidateNames(string command)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(command))
        {
            return [command];
        }

        var extensions = Environment.GetEnvironmentVariable("PATHEXT")
            ?.Split(';', StringSplitOptions.RemoveEmptyEntries)
            ?? [".COM", ".EXE", ".BAT", ".CMD"];
        return extensions.Select(extension => $"{command}{extension.ToLowerInvariant()}").ToArray();
    }
}

internal sealed partial class PortConflictResolver
{
    public async Task StopRecognisedListenerAsync(
        int port,
        IReadOnlyList<string> recognisedCommandFragments,
        CancellationToken cancellationToken)
    {
        if (!IsPortListening(port))
        {
            return;
        }

        var processIds = await FindListenerProcessIdsAsync(port, cancellationToken);
        if (processIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"Port {port} is already in use and its owner could not be identified. Stop it manually and try again.");
        }

        var owners = new List<PortOwner>();
        foreach (var processId in processIds)
        {
            var commandLine = await GetCommandLineAsync(processId, cancellationToken);
            owners.Add(new PortOwner(processId, commandLine));
        }

        var unknownOwners = owners
            .Where(owner => !IsRecognised(owner.CommandLine, recognisedCommandFragments))
            .ToArray();
        if (unknownOwners.Length > 0)
        {
            var descriptions = string.Join(
                ", ",
                unknownOwners.Select(owner => $"PID {owner.ProcessId} ({owner.CommandLine})"));
            throw new InvalidOperationException(
                $"Port {port} is owned by an unrecognised process: {descriptions}. Stop it manually and try again.");
        }

        foreach (var owner in owners)
        {
            await StopProcessAsync(owner.ProcessId, cancellationToken);
        }
    }

    internal static bool IsRecognised(string commandLine, IReadOnlyList<string> recognisedCommandFragments)
    {
        return recognisedCommandFragments.All(fragment =>
            commandLine.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPortListening(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == port);
        }
        catch (NetworkInformationException)
        {
            return true;
        }
    }

    private static async Task<IReadOnlyList<int>> FindListenerProcessIdsAsync(
        int port,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            var output = await CommandCapture.TryRunAsync(
                ExecutableLocator.Find("powershell") ?? "powershell",
                [
                    "-NoProfile",
                    "-Command",
                    $"Get-NetTCPConnection -State Listen -LocalPort {port} -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique"
                ],
                cancellationToken);
            return ParseProcessIds(output);
        }

        if (OperatingSystem.IsMacOS())
        {
            var lsof = ExecutableLocator.Find("lsof");
            if (lsof is null)
            {
                return [];
            }

            var output = await CommandCapture.TryRunAsync(
                lsof,
                ["-nP", $"-iTCP:{port}", "-sTCP:LISTEN", "-t"],
                cancellationToken);
            return ParseProcessIds(output);
        }

        var ss = ExecutableLocator.Find("ss");
        if (ss is null)
        {
            return [];
        }

        var ssOutput = await CommandCapture.TryRunAsync(ss, ["-ltnp"], cancellationToken);
        return ParseSsProcessIds(ssOutput, port);
    }

    private static IReadOnlyList<int> ParseProcessIds(string output)
    {
        return output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => int.TryParse(line, out var processId) ? processId : 0)
            .Where(processId => processId > 0)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<int> ParseSsProcessIds(string output, int port)
    {
        var processIds = new HashSet<int>();
        foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!PortPattern(port).IsMatch(line))
            {
                continue;
            }

            foreach (Match match in ProcessIdPattern().Matches(line))
            {
                if (int.TryParse(match.Groups[1].Value, out var processId))
                {
                    processIds.Add(processId);
                }
            }
        }

        return processIds.ToArray();
    }

    private static async Task<string> GetCommandLineAsync(int processId, CancellationToken cancellationToken)
    {
        string? commandLine = null;
        if (OperatingSystem.IsLinux())
        {
            var commandLinePath = $"/proc/{processId}/cmdline";
            if (File.Exists(commandLinePath))
            {
                var bytes = await File.ReadAllBytesAsync(commandLinePath, cancellationToken);
                commandLine = Encoding.UTF8.GetString(bytes).Replace('\0', ' ').Trim();
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            commandLine = await CommandCapture.TryRunAsync(
                ExecutableLocator.Find("powershell") ?? "powershell",
                [
                    "-NoProfile",
                    "-Command",
                    $"(Get-CimInstance Win32_Process -Filter \"ProcessId = {processId}\" -ErrorAction SilentlyContinue).CommandLine"
                ],
                cancellationToken);
        }
        else if (OperatingSystem.IsMacOS())
        {
            var ps = ExecutableLocator.Find("ps") ?? "ps";
            commandLine = await CommandCapture.TryRunAsync(
                ps,
                ["-p", processId.ToString(), "-o", "command="],
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            return commandLine.Trim();
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "exited process";
        }
    }

    private static async Task StopProcessAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static Regex PortPattern(int port) =>
        new($@":{port}\s", RegexOptions.CultureInvariant);

    [GeneratedRegex("pid=(\\d+)")]
    private static partial Regex ProcessIdPattern();

    private sealed record PortOwner(int ProcessId, string CommandLine);
}

internal static class CommandCapture
{
    public static async Task<string> TryRunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return string.Empty;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return await outputTask;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }
}
