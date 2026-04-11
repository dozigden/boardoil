using BoardOil.Api.Mcp;
using BoardOil.Contracts.Auth;
using System.Security.Claims;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpAuthorisationServiceTests
{
    private readonly McpAuthorisationService _service = new();

    [Fact]
    public void GetPatAccessContext_ForNonPatPrincipal_ShouldReturnNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("boardoil_auth_type", "jwt")
        ], "test"));

        // Act
        var context = _service.GetPatAccessContext(principal);

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public void GetPatAccessContext_ForPatPrincipal_ShouldParseScopes()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("boardoil_auth_type", "pat"),
            new Claim("boardoil_pat_scope", MachinePatScopes.McpRead),
            new Claim("boardoil_pat_scope", MachinePatScopes.McpWrite)
        ], "test"));

        // Act
        var context = _service.GetPatAccessContext(principal);

        // Assert
        Assert.NotNull(context);
        Assert.Contains(MachinePatScopes.McpRead, context!.Scopes);
        Assert.Contains(MachinePatScopes.McpWrite, context.Scopes);
    }

    [Fact]
    public void EnsurePatToolAccess_WhenScopeMissing_ShouldReturnForbiddenError()
    {
        // Arrange
        var context = new PatAccessContext(
            new HashSet<string>(StringComparer.Ordinal) { MachinePatScopes.McpRead });

        // Act
        var error = _service.EnsurePatToolAccess(context, MachinePatScopes.McpWrite, 1);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("forbidden", error!.Code);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public void EnsurePatToolAccess_WhenScopeIsAllowed_ShouldReturnNull()
    {
        // Arrange
        var context = new PatAccessContext(
            new HashSet<string>(StringComparer.Ordinal) { MachinePatScopes.McpWrite });

        // Act
        var error = _service.EnsurePatToolAccess(context, MachinePatScopes.McpWrite, 1);

        // Assert
        Assert.Null(error);
    }
}
