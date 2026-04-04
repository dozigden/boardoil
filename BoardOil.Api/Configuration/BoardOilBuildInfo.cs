using System.Reflection;

namespace BoardOil.Api.Configuration;

public sealed record BoardOilBuildInfo(
    string Version,
    string Channel,
    string Build,
    string Commit)
{
    public static BoardOilBuildInfo FromConfiguration(
        IConfiguration configuration,
        IHostEnvironment environment,
        Assembly assembly)
    {
        var section = configuration.GetSection("BoardOilBuild");
        var version = SanitiseVersion(Normalise(section["Version"]) ?? ResolveAssemblyVersion(assembly) ?? "0.0.0");
        var channel = (Normalise(section["Channel"]) ?? ResolveDefaultChannel(environment)).ToLowerInvariant();
        var build = Normalise(section["Build"]) ?? ResolveDefaultBuild(environment);
        var commit = Normalise(section["Commit"]) ?? "unknown";

        return new BoardOilBuildInfo(version, channel, build, commit);
    }

    private static string? ResolveAssemblyVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Trim();
        }

        return assembly.GetName().Version?.ToString();
    }

    private static string ResolveDefaultChannel(IHostEnvironment environment) =>
        environment.IsDevelopment() ? "dev" : "release";

    private static string ResolveDefaultBuild(IHostEnvironment environment) =>
        environment.IsDevelopment() ? "local" : "unknown";

    private static string SanitiseVersion(string version)
    {
        var trimmed = version.Trim();
        var plusIndex = trimmed.IndexOf('+');
        return plusIndex >= 0 ? trimmed[..plusIndex] : trimmed;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
