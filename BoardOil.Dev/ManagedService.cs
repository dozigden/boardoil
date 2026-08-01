using System.Collections.Concurrent;
using System.Diagnostics;

namespace BoardOil.Dev;

internal sealed record ServiceProcessState(string Text, bool HasFailed)
{
    public static ServiceProcessState Resolve(
        bool hasProcess,
        bool isRunning,
        int? exitCode,
        bool stoppedByUser)
    {
        if (!hasProcess || stoppedByUser || exitCode == 0)
        {
            return new ServiceProcessState("stopped", false);
        }

        if (isRunning)
        {
            return new ServiceProcessState("running", false);
        }

        return new ServiceProcessState($"exit {exitCode}", true);
    }
}

internal sealed class RecentLogBuffer(int capacity)
{
    private readonly ConcurrentQueue<string> lines = new();

    public int Count => lines.Count;

    public void Add(string line)
    {
        lines.Enqueue(line);
        while (lines.Count > capacity)
        {
            lines.TryDequeue(out _);
        }
    }

    public IReadOnlyList<string> Tail(int count)
    {
        var snapshot = lines.ToArray();
        return snapshot.Skip(Math.Max(0, snapshot.Length - count)).ToArray();
    }
}

internal sealed class ManagedService
{
    private readonly string command;
    private readonly IReadOnlyList<string> arguments;
    private readonly string workingDirectory;
    private readonly IReadOnlyDictionary<string, string> environment;
    private readonly Func<ManagedService, CancellationToken, Task> beforeStart;
    private readonly RecentLogBuffer recentLogs = new(500);
    private readonly SemaphoreSlim logLock = new(1, 1);
    private readonly object sync = new();
    private Process? process;
    private DateTimeOffset? startedAt;
    private bool stoppedByUser;

    public ManagedService(
        string name,
        string endpoint,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string logPath,
        IReadOnlyDictionary<string, string> environment,
        Func<ManagedService, CancellationToken, Task> beforeStart)
    {
        Name = name;
        Endpoint = endpoint;
        this.command = command;
        this.arguments = arguments;
        this.workingDirectory = workingDirectory;
        LogPath = logPath;
        this.environment = environment;
        this.beforeStart = beforeStart;
    }

    public string Name { get; }
    public string Endpoint { get; }
    public string LogPath { get; }

    public bool IsRunning
    {
        get
        {
            lock (sync)
            {
                return process is { HasExited: false };
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (sync)
            {
                return process is { HasExited: false } ? process.Id : null;
            }
        }
    }

    public TimeSpan Uptime
    {
        get
        {
            if (startedAt is null || !IsRunning)
            {
                return TimeSpan.Zero;
            }

            return DateTimeOffset.UtcNow - startedAt.Value;
        }
    }

    public ServiceProcessState State
    {
        get
        {
            lock (sync)
            {
                if (process is null)
                {
                    return ServiceProcessState.Resolve(false, false, null, stoppedByUser);
                }

                if (!process.HasExited)
                {
                    return ServiceProcessState.Resolve(true, true, null, stoppedByUser);
                }

                return ServiceProcessState.Resolve(true, false, process.ExitCode, stoppedByUser);
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        await beforeStart(this, cancellationToken);

        var startInfo = CreateStartInfo(command, arguments, workingDirectory, environment);
        var nextProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {Name}.");
        nextProcess.StandardInput.Close();

        lock (sync)
        {
            process?.Dispose();
            process = nextProcess;
            startedAt = DateTimeOffset.UtcNow;
            stoppedByUser = false;
        }

        await AddLogLineAsync($"$ {FormatCommand(command, arguments)}", cancellationToken);
        _ = Task.Run(
            () => CaptureOutputAsync(nextProcess, nextProcess.StandardOutput, cancellationToken),
            CancellationToken.None);
        _ = Task.Run(
            () => CaptureOutputAsync(nextProcess, nextProcess.StandardError, cancellationToken),
            CancellationToken.None);
        _ = Task.Run(() => MonitorExitAsync(nextProcess), CancellationToken.None);
    }

    public async Task StopAsync()
    {
        Process? current;
        lock (sync)
        {
            current = process;
            stoppedByUser = true;
        }

        if (current is null || current.HasExited)
        {
            return;
        }

        try
        {
            current.Kill(entireProcessTree: true);
            await current.WaitForExitAsync();
            await AddLogLineAsync($"Stopped {Name}.", CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public IReadOnlyList<string> GetLogLines(int count) => recentLogs.Tail(count);

    public int VisibleLogLineCount(int count) => Math.Min(count, recentLogs.Count);

    public async Task RunPreparationCommandAsync(
        string preparationCommand,
        IReadOnlyList<string> preparationArguments,
        string preparationWorkingDirectory,
        CancellationToken cancellationToken)
    {
        await AddLogLineAsync(
            $"$ {FormatCommand(preparationCommand, preparationArguments)}",
            cancellationToken);

        var startInfo = CreateStartInfo(
            preparationCommand,
            preparationArguments,
            preparationWorkingDirectory,
            new Dictionary<string, string>());
        using var preparationProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {preparationCommand}.");
        preparationProcess.StandardInput.Close();

        var stdout = CaptureOutputAsync(
            preparationProcess,
            preparationProcess.StandardOutput,
            cancellationToken);
        var stderr = CaptureOutputAsync(
            preparationProcess,
            preparationProcess.StandardError,
            cancellationToken);
        try
        {
            await preparationProcess.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            if (!preparationProcess.HasExited)
            {
                preparationProcess.Kill(entireProcessTree: true);
                await preparationProcess.WaitForExitAsync(CancellationToken.None);
            }

            throw;
        }

        if (preparationProcess.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{preparationCommand} exited with code {preparationProcess.ExitCode}.");
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string processCommand,
        IReadOnlyList<string> processArguments,
        string processWorkingDirectory,
        IReadOnlyDictionary<string, string> processEnvironment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = processCommand,
            WorkingDirectory = processWorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in processArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in processEnvironment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        return startInfo;
    }

    private async Task CaptureOutputAsync(
        Process owner,
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                await AddLogLineAsync(line, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException) when (owner.HasExited)
        {
        }
    }

    private async Task MonitorExitAsync(Process owner)
    {
        try
        {
            await owner.WaitForExitAsync();
            if (IsCurrentProcess(owner) && !stoppedByUser)
            {
                await AddLogLineAsync(
                    $"{Name} exited with code {owner.ExitCode}.",
                    CancellationToken.None);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private bool IsCurrentProcess(Process candidate)
    {
        lock (sync)
        {
            return ReferenceEquals(process, candidate);
        }
    }

    private async Task AddLogLineAsync(string line, CancellationToken cancellationToken)
    {
        var timestampedLine = $"{DateTimeOffset.Now:HH:mm:ss} {line}";
        recentLogs.Add(timestampedLine);
        await logLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                LogPath,
                $"{timestampedLine}{Environment.NewLine}",
                cancellationToken);
        }
        finally
        {
            logLock.Release();
        }
    }

    private static string FormatCommand(string processCommand, IReadOnlyList<string> processArguments)
    {
        return $"{processCommand} {string.Join(' ', processArguments.Select(QuoteArgument))}";
    }

    private static string QuoteArgument(string argument)
    {
        return argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
    }
}
