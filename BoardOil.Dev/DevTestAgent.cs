using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace BoardOil.Dev;

internal sealed record DevAgentInheritedServer(string Name, bool IsStdio);

internal static class DevTestAgent
{
    public const string TokenEnvironmentVariable = "BOARDOIL_DEV_MCP_TOKEN";

    public static void EnsureAvailable()
    {
        if (FindCodex() is null)
        {
            throw new InvalidOperationException("Could not find Codex on PATH or in the installed desktop package.");
        }
    }

    private static string? FindCodex()
    {
        var codexRoot = Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        return ExecutableLocator.Find("codex") ?? FindBundledCodex(codexRoot);
    }

    internal static string? FindBundledCodex(string codexRoot)
    {
        var executable = "codex";
        if (OperatingSystem.IsWindows())
        {
            executable += ".exe";
        }
        var path = Path.Combine(codexRoot, "packages", "app-server-daemon", "current", executable);
        return File.Exists(path) ? path : null;
    }

    public static async Task RunAsync(string repoRoot, string databasePath, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        var codex = FindCodex()!;
        var dotnet = ExecutableLocator.Find("dotnet") ?? "dotnet";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        using var readiness = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readiness.CancelAfter(TimeSpan.FromSeconds(60));
        await WaitForApiAsync(http, readiness.Token);

        var directory = Directory.CreateTempSubdirectory("boardoil-mcp-agent-").FullName;
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var sessionPath = Path.Combine(directory, "session.json");
        var workspace = Directory.CreateDirectory(Path.Combine(directory, "workspace")).FullName;
        var provisioned = false;
        try
        {
            var configuration = new ProcessStartInfo(codex) { WorkingDirectory = workspace, UseShellExecute = false };
            configuration.ArgumentList.Add("mcp");
            configuration.ArgumentList.Add("list");
            configuration.ArgumentList.Add("--json");
            using var configuredServers = JsonDocument.Parse(await CaptureAsync(configuration, cancellationToken));
            var servers = configuredServers.RootElement.EnumerateArray()
                .Select(server => new DevAgentInheritedServer(server.GetProperty("name").GetString()!,
                    server.GetProperty("transport").GetProperty("type").GetString() == "stdio")).ToArray();

            Console.WriteLine("Preparing local MCP credentials...");
            await RunSessionCommandAsync(dotnet, repoRoot, databasePath, sessionPath, revoke: false, cancellationToken);
            provisioned = true;
            using var session = JsonDocument.Parse(await File.ReadAllTextAsync(sessionPath, cancellationToken));
            var token = session.RootElement.GetProperty("Token").GetString()!;
            if (session.RootElement.GetProperty("InitialSetup").GetBoolean())
            {
                Console.WriteLine("Fresh development database: web login is dev / BoardOilDev1234!");
            }

            var serverName = $"boardoil_dev_{Guid.NewGuid():N}";
            var instructions = $"""
                You are an interactive BoardOil MCP test agent, connected to the local development instance.
                Use only the {serverName} MCP server for BoardOil operations. Its endpoint is http://127.0.0.1:5000/mcp.
                This is development test data. The default board is boardId 1. Discover boards and options as needed.
                Exercise the actual MCP tools; do not substitute REST calls, database access or shell scripts.
                Do not change source code or access production services. Follow the user's testing requests.
                Report tool errors clearly. For card references, use the card type emoji, number and title supplied by BoardOil.
                """;
            await File.WriteAllTextAsync(Path.Combine(workspace, "AGENTS.md"), instructions, cancellationToken);
            var agent = CreateStartInfo(codex, workspace, serverName, servers, token);
            Console.WriteLine("Opening a fresh development test agent. Exit Codex to return to the dev dashboard.");
            using var process = Process.Start(agent)
                ?? throw new InvalidOperationException("Could not start the MCP test agent.");
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"The MCP test agent exited with code {process.ExitCode}.");
            }
        }
        finally
        {
            try
            {
                if (provisioned)
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    await RunSessionCommandAsync(dotnet, repoRoot, databasePath, sessionPath, revoke: true, cleanup.Token);
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    internal static ProcessStartInfo CreateStartInfo(
        string codex, string workspace, string serverName, IReadOnlyList<DevAgentInheritedServer> inheritedServers, string token)
    {
        var startInfo = new ProcessStartInfo(codex) { WorkingDirectory = workspace, UseShellExecute = false };
        startInfo.ArgumentList.Add("--no-daemon");
        startInfo.ArgumentList.Add("--cd");
        startInfo.ArgumentList.Add(workspace);
        startInfo.ArgumentList.Add("--sandbox");
        startInfo.ArgumentList.Add("read-only");
        // Quote table keys inside the TOML value: Codex's dotted override paths do not parse quoted keys.
        var servers = inheritedServers.Select(server =>
            {
                // Supply a valid transport even for servers injected outside config.toml.
                var transport = "url=\"http://127.0.0.1:5000/mcp\"";
                if (server.IsStdio)
                {
                    transport = "command=\"false\"";
                }
                return $"{JsonSerializer.Serialize(server.Name)}={{{transport},enabled=false}}";
            })
            .Append($"{JsonSerializer.Serialize(serverName)}={{url=\"http://127.0.0.1:5000/mcp\",bearer_token_env_var=\"{TokenEnvironmentVariable}\",enabled=true,required=true}}");
        AddOverride($"mcp_servers={{{string.Join(',', servers)}}}");
        AddOverride($"projects={{{JsonSerializer.Serialize(workspace)}={{trust_level=\"trusted\"}}}}");
        startInfo.Environment[TokenEnvironmentVariable] = token;
        startInfo.ArgumentList.Add("You are ready to test the local BoardOil MCP tools. Briefly confirm the dev connection using board_get for board 1, then wait for my testing instructions.");
        return startInfo;

        void AddOverride(string value)
        {
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add(value);
        }
    }

    private static async Task WaitForApiAsync(HttpClient client, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync($"{DevApiLaunchSettings.HttpEndpoint}/api/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(250, cancellationToken);
        }
    }

    private static async Task RunSessionCommandAsync(
        string dotnet, string repoRoot, string databasePath, string sessionPath, bool revoke, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(dotnet) { WorkingDirectory = Path.Combine(repoRoot, "BoardOil.Api"), UseShellExecute = false };
        startInfo.ArgumentList.Add(Path.Combine(repoRoot, "BoardOil.Api", "bin", "Debug", "net10.0", "BoardOil.Api.dll"));
        startInfo.ArgumentList.Add(revoke ? "--BoardOilDev:RevokeMcpSession" : "--BoardOilDev:CreateMcpSession");
        startInfo.ArgumentList.Add(sessionPath);
        foreach (var (key, value) in DevApiLaunchSettings.CreateEnvironment(databasePath))
        {
            startInfo.Environment[key] = value;
        }
        _ = await CaptureAsync(startInfo, cancellationToken);
    }

    private static async Task<string> CaptureAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(output, errors);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Test-agent setup failed: {await errors}\n{await output}");
            }
            return await output;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
