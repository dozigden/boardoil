namespace BoardOil.Dev;

internal sealed class DevOrchestrator
{
    private readonly string repoRoot;
    private readonly DevDatabaseManager databaseManager;
    private readonly PortConflictResolver portConflictResolver = new();
    private readonly List<ManagedService> services;
    private readonly CancellationTokenSource shutdown = new();
    private Task? activeOperation;
    private int selectedServiceIndex;
    private int selectedLogIndex;
    private string statusMessage = "Ready.";

    public DevOrchestrator(string repoRoot, DevDatabaseManager databaseManager)
    {
        this.repoRoot = repoRoot;
        this.databaseManager = databaseManager;
        services =
        [
            CreateApiService(),
            CreateWebService()
        ];
    }

    public async Task<int> RunAsync()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Error.WriteLine("BoardOil dev orchestrator requires an interactive terminal. Use dev-startall for unattended startup.");
            return 1;
        }

        var originalCancelHandling = Console.TreatControlCAsInput;
        Console.TreatControlCAsInput = true;

        try
        {
            Console.CursorVisible = false;
            await DrawLoopAsync(shutdown.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        finally
        {
            shutdown.Cancel();
            await AwaitActiveOperationAsync();
            await StopAllAsync();
            Console.CursorVisible = true;
            Console.TreatControlCAsInput = originalCancelHandling;
            Console.ResetColor();
            Console.Clear();
        }
    }

    private async Task DrawLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Render();

            var delayTask = Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            while (Console.KeyAvailable)
            {
                HandleKey(Console.ReadKey(intercept: true), cancellationToken);
                Render();
            }

            await delayTask;
        }
    }

    private void HandleKey(ConsoleKeyInfo key, CancellationToken cancellationToken)
    {
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C)
        {
            shutdown.Cancel();
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.K:
                SelectService(Math.Max(0, selectedServiceIndex - 1));
                break;
            case ConsoleKey.DownArrow:
            case ConsoleKey.J:
                SelectService(Math.Min(services.Count - 1, selectedServiceIndex + 1));
                break;
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                SelectService(0);
                break;
            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                SelectService(1);
                break;
            case ConsoleKey.Spacebar:
                BeginOperation(() => ToggleSelectedAsync(cancellationToken));
                break;
            case ConsoleKey.R:
                BeginOperation(() => RestartSelectedAsync(cancellationToken));
                break;
            case ConsoleKey.A:
                BeginOperation(() => StartAllAsync(cancellationToken));
                break;
            case ConsoleKey.X:
                BeginOperation(StopAllWithStatusAsync);
                break;
            case ConsoleKey.B:
                BeginOperation(() => UseDatabaseAsync(DevDatabaseMode.Branch, cancellationToken));
                break;
            case ConsoleKey.S:
                BeginOperation(() => UseDatabaseAsync(DevDatabaseMode.Shared, cancellationToken));
                break;
            case ConsoleKey.T:
                BeginOperation(() => UseDatabaseAsync(DevDatabaseMode.Temporary, cancellationToken));
                break;
            case ConsoleKey.L:
                selectedLogIndex = (selectedLogIndex + 1) % services.Count;
                break;
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                shutdown.Cancel();
                break;
        }
    }

    private void BeginOperation(Func<Task> operation)
    {
        if (activeOperation is { IsCompleted: false })
        {
            statusMessage = "A service operation is already in progress.";
            return;
        }

        activeOperation = RunOperationAsync(operation);
    }

    private async Task RunOperationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            statusMessage = $"Operation failed: {exception.Message}";
        }
    }

    private async Task AwaitActiveOperationAsync()
    {
        if (activeOperation is null)
        {
            return;
        }

        try
        {
            await activeOperation;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task StopAllWithStatusAsync()
    {
        await StopAllAsync();
        statusMessage = "Stopped all services.";
    }

    private void SelectService(int index)
    {
        selectedServiceIndex = index;
        selectedLogIndex = index;
    }

    private async Task UseDatabaseAsync(DevDatabaseMode mode, CancellationToken cancellationToken)
    {
        if (databaseManager.Current.Mode == mode && mode != DevDatabaseMode.Temporary)
        {
            statusMessage = $"Already using the {databaseManager.Current.DisplayName} database.";
            return;
        }

        var apiWasRunning = services[0].IsRunning;
        await services[0].StopAsync();
        databaseManager.Select(mode);
        services[0] = CreateApiService();
        statusMessage = $"Using the {databaseManager.Current.DisplayName} database.";

        if (apiWasRunning)
        {
            await StartServiceAsync(services[0], cancellationToken);
        }
    }

    private async Task ToggleSelectedAsync(CancellationToken cancellationToken)
    {
        var service = services[selectedServiceIndex];
        if (service.IsRunning)
        {
            await service.StopAsync();
            statusMessage = $"Stopped {service.Name}.";
            return;
        }

        await StartServiceAsync(service, cancellationToken);
    }

    private async Task RestartSelectedAsync(CancellationToken cancellationToken)
    {
        var service = services[selectedServiceIndex];
        await service.StopAsync();
        await StartServiceAsync(service, cancellationToken);
    }

    private async Task StartAllAsync(CancellationToken cancellationToken)
    {
        foreach (var service in services)
        {
            if (!service.IsRunning)
            {
                await StartServiceAsync(service, cancellationToken);
            }
        }
    }

    private async Task StopAllAsync()
    {
        foreach (var service in services)
        {
            await service.StopAsync();
        }
    }

    private async Task StartServiceAsync(ManagedService service, CancellationToken cancellationToken)
    {
        try
        {
            statusMessage = $"Starting {service.Name}...";
            await service.StartAsync(cancellationToken);
            statusMessage = $"Started {service.Name}.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            statusMessage = $"Failed to start {service.Name}: {exception.Message}";
        }
    }

    private ManagedService CreateApiService()
    {
        var apiProject = Path.Combine(repoRoot, "BoardOil.Api", "BoardOil.Api.csproj");
        var logPath = Path.Combine(repoRoot, ".data", "dev", "logs", "api.log");
        var dotnet = ExecutableLocator.Find("dotnet") ?? "dotnet";
        var databasePath = databaseManager.Current.DatabasePath;

        return new ManagedService(
            "API",
            DevApiLaunchSettings.HttpsEndpoint,
            dotnet,
            DevApiLaunchSettings.CreateRunArguments(apiProject),
            repoRoot,
            logPath,
            DevApiLaunchSettings.CreateEnvironment(databasePath),
            async (service, cancellationToken) =>
            {
                EnsureExecutableExists("dotnet", dotnet);
                databaseManager.PrepareCurrentDatabase();
                foreach (var port in DevApiLaunchSettings.Ports)
                {
                    await portConflictResolver.StopRecognisedListenerAsync(
                        port,
                        ["BoardOil.Api"],
                        cancellationToken);
                }

                await service.RunPreparationCommandAsync(
                    dotnet,
                    ["dev-certs", "https"],
                    repoRoot,
                    cancellationToken);
                await service.RunPreparationCommandAsync(
                    dotnet,
                    ["build", apiProject, "-maxcpucount:1", "-nodeReuse:false"],
                    repoRoot,
                    cancellationToken);
            });
    }

    private ManagedService CreateWebService()
    {
        var webDirectory = Path.Combine(repoRoot, "BoardOil.Web");
        var logPath = Path.Combine(repoRoot, ".data", "dev", "logs", "web.log");
        var npm = ExecutableLocator.Find("npm") ?? "npm";

        return new ManagedService(
            "Web",
            "http://localhost:5173",
            npm,
            ["run", "dev"],
            webDirectory,
            logPath,
            new Dictionary<string, string>(),
            async (_, cancellationToken) =>
            {
                EnsureExecutableExists("npm", npm);
                var nodeModules = Path.Combine(webDirectory, "node_modules");
                if (!Directory.Exists(nodeModules))
                {
                    throw new InvalidOperationException(
                        "BoardOil.Web/node_modules is missing. Run 'cd BoardOil.Web && npm ci' first.");
                }

                await portConflictResolver.StopRecognisedListenerAsync(
                    5173,
                    ["vite"],
                    cancellationToken);
            });
    }

    private static void EnsureExecutableExists(string displayName, string resolvedPath)
    {
        if (ExecutableLocator.Find(displayName) is null && !File.Exists(resolvedPath))
        {
            throw new InvalidOperationException($"{displayName} is required but was not found on PATH.");
        }
    }

    private void Render()
    {
        var width = Math.Max(1, Console.WindowWidth - 1);
        var height = Math.Max(1, Console.WindowHeight);
        var logHeight = Math.Max(5, height - 13 - services.Count);

        Console.SetCursorPosition(0, 0);
        WriteLine("BoardOil Dev Orchestrator", width, ConsoleColor.Cyan);
        WriteLine(new string('=', Math.Min(width, 120)), width, ConsoleColor.DarkGray);
        WriteLine(
            $"Database: {databaseManager.Current.DisplayName} [{databaseManager.GetDisplayPath()}]",
            width,
            DatabaseColour());
        WriteLine("Services", width, ConsoleColor.White);

        for (var index = 0; index < services.Count; index++)
        {
            var service = services[index];
            var selector = index == selectedServiceIndex ? ">" : " ";
            var marker = GetStateMarker(service.State);
            var processId = service.ProcessId?.ToString() ?? "-";
            var uptime = service.IsRunning ? FormatDuration(service.Uptime) : "--:--";
            var line = $"{selector} {index + 1}. {marker} {service.Name,-5} {service.State.Text,-10} pid {processId,-7} up {uptime,-8} {service.Endpoint}";
            WriteLine(line, width, ServiceColour(service.State));
        }

        WriteLine(string.Empty, width);
        WriteLine(
            "Keys: Up/Down  Space start/stop  R restart  A start all  X stop all  B branch DB  S shared DB  T new temp DB  L logs  1-2 select  Q quit",
            width,
            ConsoleColor.Yellow);
        WriteLine($"Status: {statusMessage}", width, ConsoleColor.White);
        WriteLine(string.Empty, width);

        var logService = services[selectedLogIndex];
        WriteLine($"Logs: {logService.Name} ({logService.LogPath})", width, ConsoleColor.White);
        WriteLine(new string('-', Math.Min(width, 120)), width, ConsoleColor.DarkGray);

        foreach (var line in logService.GetLogLines(logHeight))
        {
            WriteLine(line, width, ConsoleColor.DarkGray);
        }

        for (var index = logService.VisibleLogLineCount(logHeight); index < logHeight; index++)
        {
            WriteLine(string.Empty, width);
        }
    }

    private ConsoleColor DatabaseColour()
    {
        if (databaseManager.Current.Mode == DevDatabaseMode.Temporary)
        {
            return ConsoleColor.Yellow;
        }

        return ConsoleColor.Gray;
    }

    private static string GetStateMarker(ServiceProcessState state)
    {
        if (state.HasFailed)
        {
            return "[!!]";
        }

        return state.Text == "running" ? "[OK]" : "[--]";
    }

    private static ConsoleColor ServiceColour(ServiceProcessState state)
    {
        if (state.HasFailed)
        {
            return ConsoleColor.Red;
        }

        return state.Text == "running" ? ConsoleColor.Green : ConsoleColor.Gray;
    }

    private static void WriteLine(string text, int width, ConsoleColor? colour = null)
    {
        if (colour is not null)
        {
            Console.ForegroundColor = colour.Value;
        }

        var clipped = text.Length > width ? text[..width] : text;
        Console.Write(clipped);
        if (clipped.Length < width)
        {
            Console.Write(new string(' ', width - clipped.Length));
        }

        Console.WriteLine();
        if (colour is not null)
        {
            Console.ResetColor();
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        return $"{duration.Minutes:00}:{duration.Seconds:00}";
    }
}
