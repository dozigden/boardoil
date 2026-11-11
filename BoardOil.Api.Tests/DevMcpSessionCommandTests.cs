using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Development;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class DevMcpSessionCommandTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public DevMcpSessionCommandTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task ProvisionedTokenShouldUseNormalMcpAuthenticationAndBeRevokedAfterwards()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var directory = Directory.CreateTempSubdirectory("boardoil-dev-session-test-").FullName;
        var path = Path.Combine(directory, "session.json");
        try
        {
            // Act
            await RunCommandAsync(DevMcpSessionCommand.CreateKey, path);
            using var session = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var token = session.RootElement.GetProperty("Token").GetString()!;
            using var allowed = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "dev-session", token);
            await RunCommandAsync(DevMcpSessionCommand.RevokeKey, path);
            using var revoked = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "revoked-session", token);

            // Assert
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
            Assert.False(session.RootElement.GetProperty("InitialSetup").GetBoolean());
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FreshDatabaseShouldBeReadyWithoutManualAccountSetup()
    {
        // Arrange
        var client = CreateClient();
        var directory = Directory.CreateTempSubdirectory("boardoil-dev-session-test-").FullName;
        var path = Path.Combine(directory, "session.json");
        try
        {
            // Act
            await RunCommandAsync(DevMcpSessionCommand.CreateKey, path);
            using var session = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            using var login = await client.PostAsJsonAsync("/api/auth/machine/login", new
            {
                userName = DevMcpSessionCommand.InitialUserName,
                password = DevMcpSessionCommand.InitialPassword
            });
            using var board = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
                new { name = "board_get", arguments = new { id = 1 } }, "fresh-dev-board", session.RootElement.GetProperty("Token").GetString());

            // Assert
            Assert.True(session.RootElement.GetProperty("InitialSetup").GetBoolean());
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            using var payload = await McpJsonRpcClient.ParseJsonAsync(board);
            Assert.Equal(1, McpJsonRpcClient.GetStructuredContent(payload).GetProperty("id").GetInt32());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("Production", DevMcpSessionCommand.CreateKey)]
    [InlineData("Production", DevMcpSessionCommand.RevokeKey)]
    [InlineData("Testing", DevMcpSessionCommand.CreateKey)]
    public async Task CommandsOutsideDevelopmentShouldFailBeforeResolvingServices(string environment, string key)
    {
        // Arrange
        using var services = new ServiceCollection().BuildServiceProvider();
        var configuration = Configuration(key, "/unused/session.json");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevMcpSessionCommand.ExecuteAsync(services, configuration, new HostingEnvironment { EnvironmentName = environment }));

        // Assert
        Assert.Contains("only available in Development", exception.Message);
    }

    [Fact]
    public async Task FailedCredentialFileWriteShouldRevokeTheNewTokenAndPreserveExistingFile()
    {
        // Arrange
        _ = CreateClient();
        await EnsureInitialAdminSeededAsync();
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "existing file");
        try
        {
            // Act
            await Assert.ThrowsAsync<IOException>(() => RunCommandAsync(DevMcpSessionCommand.CreateKey, path));

            // Assert
            await using var db = Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
            var token = await db.PersonalAccessTokens.SingleAsync();
            Assert.NotNull(token.RevokedAtUtc);
            Assert.Equal("existing file", await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private Task RunCommandAsync(string key, string path) =>
        DevMcpSessionCommand.ExecuteAsync(Factory.Services, Configuration(key, path),
            new HostingEnvironment { EnvironmentName = Environments.Development });

    private static IConfiguration Configuration(string key, string path) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = path }).Build();
}
