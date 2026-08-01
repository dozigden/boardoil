using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BoardOil.Dev;

internal enum DevDatabaseMode
{
    Branch,
    Shared,
    Temporary
}

internal sealed record DevDatabaseSelection(
    DevDatabaseMode Mode,
    string DisplayName,
    string DatabasePath,
    string? TemporaryDirectory);

internal sealed class DevDatabaseManager : IDisposable
{
    private readonly string repoRoot;
    private readonly string mainDatabasePath;
    private readonly string mainBranchName;
    private readonly string currentBranchName;
    private readonly bool seedFromMain;
    private string? temporaryDirectory;

    public DevDatabaseManager(string repoRoot)
    {
        this.repoRoot = repoRoot;
        mainDatabasePath = Path.Combine(repoRoot, ".data", "dev", "boardoil.dev.db");
        mainBranchName = Environment.GetEnvironmentVariable("BOARDOIL_MAIN_BRANCH_NAME") ?? "main";
        currentBranchName = GitBranchResolver.GetCurrentBranchName(repoRoot);
        seedFromMain = Environment.GetEnvironmentVariable("BOARDOIL_DEV_DB_SEED_FROM_MAIN") != "0";

        var configuredMode = Environment.GetEnvironmentVariable("BOARDOIL_DEV_DB_MODE");
        Current = SelectInitialMode(configuredMode);
    }

    public DevDatabaseSelection Current { get; private set; }

    public DevDatabaseSelection Select(DevDatabaseMode mode)
    {
        DeleteTemporaryDatabase();
        Current = CreateSelection(mode);
        return Current;
    }

    public void PrepareCurrentDatabase()
    {
        var databaseDirectory = Path.GetDirectoryName(Current.DatabasePath)
            ?? throw new InvalidOperationException("The development database path has no parent directory.");
        Directory.CreateDirectory(databaseDirectory);

        if (Current.Mode != DevDatabaseMode.Branch || Current.DatabasePath == mainDatabasePath)
        {
            return;
        }

        DevDatabaseSeeder.SeedIfNeeded(mainDatabasePath, Current.DatabasePath, seedFromMain);
    }

    public string GetDisplayPath()
    {
        if (Current.Mode == DevDatabaseMode.Temporary)
        {
            return Current.DatabasePath;
        }

        return Path.GetRelativePath(repoRoot, Current.DatabasePath);
    }

    public void Dispose()
    {
        DeleteTemporaryDatabase();
    }

    private DevDatabaseSelection SelectInitialMode(string? configuredMode)
    {
        if (configuredMode?.Equals("shared", StringComparison.OrdinalIgnoreCase) == true)
        {
            return CreateSelection(DevDatabaseMode.Shared);
        }

        if (configuredMode?.Equals("temporary", StringComparison.OrdinalIgnoreCase) == true
            || configuredMode?.Equals("temp", StringComparison.OrdinalIgnoreCase) == true)
        {
            return CreateSelection(DevDatabaseMode.Temporary);
        }

        return CreateSelection(DevDatabaseMode.Branch);
    }

    private DevDatabaseSelection CreateSelection(DevDatabaseMode mode)
    {
        if (mode == DevDatabaseMode.Shared)
        {
            return new DevDatabaseSelection(mode, "shared", mainDatabasePath, null);
        }

        if (mode == DevDatabaseMode.Temporary)
        {
            temporaryDirectory = Directory.CreateTempSubdirectory("boardoil-dev-").FullName;
            var databasePath = Path.Combine(temporaryDirectory, "boardoil.dev.db");
            return new DevDatabaseSelection(mode, "fresh temporary", databasePath, temporaryDirectory);
        }

        var branchPath = DevDatabasePaths.ResolveBranchDatabasePath(
            repoRoot,
            currentBranchName,
            mainBranchName);
        return new DevDatabaseSelection(mode, $"branch ({currentBranchName})", branchPath, null);
    }

    private void DeleteTemporaryDatabase()
    {
        if (temporaryDirectory is null)
        {
            return;
        }

        try
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        temporaryDirectory = null;
    }
}

internal static partial class DevDatabasePaths
{
    public static string ResolveBranchDatabasePath(
        string repoRoot,
        string currentBranchName,
        string mainBranchName)
    {
        var mainDatabasePath = Path.Combine(repoRoot, ".data", "dev", "boardoil.dev.db");
        if (currentBranchName == mainBranchName)
        {
            return mainDatabasePath;
        }

        var branchDirectoryName = SanitiseBranchName(currentBranchName);
        return Path.Combine(
            repoRoot,
            ".data",
            "dev",
            "branches",
            branchDirectoryName,
            "boardoil.dev.db");
    }

    public static string SanitiseBranchName(string branchName)
    {
        var sanitised = UnsafePathCharacterRegex().Replace(branchName, "_");
        sanitised = RepeatedUnderscoreRegex().Replace(sanitised, "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitised) ? "unknown" : sanitised;
    }

    [GeneratedRegex("[^A-Za-z0-9._-]+")]
    private static partial Regex UnsafePathCharacterRegex();

    [GeneratedRegex("_+")]
    private static partial Regex RepeatedUnderscoreRegex();
}

internal static class DevDatabaseSeeder
{
    public static void SeedIfNeeded(
        string sourceDatabasePath,
        string targetDatabasePath,
        bool enabled,
        bool preferSqliteBackup = true)
    {
        if (!enabled || File.Exists(targetDatabasePath) || !File.Exists(sourceDatabasePath))
        {
            return;
        }

        var targetDirectory = Path.GetDirectoryName(targetDatabasePath)
            ?? throw new InvalidOperationException("The branch database path has no parent directory.");
        Directory.CreateDirectory(targetDirectory);

        if (preferSqliteBackup && TryCreateSqliteBackup(sourceDatabasePath, targetDatabasePath))
        {
            return;
        }

        File.Copy(sourceDatabasePath, targetDatabasePath);
        CopySidecarIfPresent(sourceDatabasePath, targetDatabasePath, "-wal");
        CopySidecarIfPresent(sourceDatabasePath, targetDatabasePath, "-shm");
    }

    private static bool TryCreateSqliteBackup(string sourceDatabasePath, string targetDatabasePath)
    {
        var sqlite = ExecutableLocator.Find("sqlite3");
        if (sqlite is null)
        {
            return false;
        }

        try
        {
            var escapedTargetPath = targetDatabasePath.Replace("'", "''", StringComparison.Ordinal);
            var startInfo = new ProcessStartInfo
            {
                FileName = sqlite,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(sourceDatabasePath);
            startInfo.ArgumentList.Add($".backup '{escapedTargetPath}'");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WhenAll(outputTask, errorTask).GetAwaiter().GetResult();
            if (process.ExitCode == 0 && File.Exists(targetDatabasePath))
            {
                return true;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }

        DeleteIfPresent(targetDatabasePath);
        DeleteIfPresent($"{targetDatabasePath}-wal");
        DeleteIfPresent($"{targetDatabasePath}-shm");
        return false;
    }

    private static void CopySidecarIfPresent(string sourcePath, string targetPath, string suffix)
    {
        var sourceSidecar = $"{sourcePath}{suffix}";
        if (File.Exists(sourceSidecar))
        {
            File.Copy(sourceSidecar, $"{targetPath}{suffix}");
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

internal static class GitBranchResolver
{
    public static string GetCurrentBranchName(string repoRoot)
    {
        var branchName = RunGit(repoRoot, "rev-parse", "--abbrev-ref", "HEAD");
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return "unknown";
        }

        if (branchName != "HEAD")
        {
            return branchName;
        }

        var shortSha = RunGit(repoRoot, "rev-parse", "--short", "HEAD");
        return string.IsNullOrWhiteSpace(shortSha) ? "detached" : $"detached-{shortSha}";
    }

    private static string? RunGit(string repoRoot, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutableLocator.Find("git") ?? "git",
                WorkingDirectory = repoRoot,
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
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
