namespace BoardOil.Dev;

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            var repoRoot = RepoRootLocator.Find();
            using var databaseManager = new DevDatabaseManager(repoRoot);
            var orchestrator = new DevOrchestrator(repoRoot, databaseManager);

            return await orchestrator.RunAsync();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"BoardOil dev orchestrator failed: {exception.Message}");
            return 1;
        }
    }
}

internal static class RepoRootLocator
{
    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BoardOil.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find BoardOil.slnx.");
    }
}
