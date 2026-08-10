using BoardOil.Api.Mcp;
using BoardOil.Contracts.Auth;
using OpenIddict.Abstractions;
using System.Security.Claims;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpAuthorisationServiceTests
{
    private readonly McpAuthorisationService _service = new();

    [Fact]
    public void GetAccessContext_ForNonMachinePrincipal_ShouldReturnNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("boardoil_auth_type", "jwt")
        ], "test"));

        // Act
        var context = _service.GetAccessContext(principal);

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public void GetAccessContext_ForPatPrincipal_ShouldParseActorAndScopes()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("boardoil_auth_type", "pat"),
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim("boardoil_pat_scope", MachinePatScopes.McpRead),
            new Claim("boardoil_pat_scope", MachinePatScopes.McpWrite)
        ], "test"));

        // Act
        var context = _service.GetAccessContext(principal);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(42, context!.ActorUserId);
        Assert.Contains(MachinePatScopes.McpRead, context.Scopes);
        Assert.Contains(MachinePatScopes.McpWrite, context.Scopes);
    }

    [Fact]
    public void GetAccessContext_ForOAuthPrincipal_ShouldParseActorAndScopes()
    {
        // Arrange
        var identity = new ClaimsIdentity(
        [
            new Claim("boardoil_user_id", "57")
        ], "test");
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(MachinePatScopes.McpRead);

        // Act
        var context = _service.GetAccessContext(principal);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(57, context!.ActorUserId);
        Assert.Equal("OAuth", context.AuthenticationType);
        Assert.Contains(MachinePatScopes.McpRead, context.Scopes);
    }

    [Fact]
    public void EnsureToolAccess_WhenScopeMissing_ShouldReturnForbiddenError()
    {
        // Arrange
        var context = new McpAccessContext(
            42,
            "PAT",
            new HashSet<string>(StringComparer.Ordinal) { MachinePatScopes.McpRead });

        // Act
        var error = _service.EnsureToolAccess(context, MachinePatScopes.McpWrite, 1);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("forbidden", error!.Code);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public void EnsureToolAccess_WhenScopeIsAllowed_ShouldReturnNull()
    {
        // Arrange
        var context = new McpAccessContext(
            42,
            "PAT",
            new HashSet<string>(StringComparer.Ordinal) { MachinePatScopes.McpWrite });

        // Act
        var error = _service.EnsureToolAccess(context, MachinePatScopes.McpWrite, 1);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void EnsureScopeAccess_WhenToolDoesNotRequireScope_ShouldReturnNull()
    {
        // Arrange
        var context = new McpAccessContext(
            42,
            "OAuth",
            new HashSet<string>(StringComparer.Ordinal) { MachinePatScopes.McpWrite });

        // Act
        var error = _service.EnsureScopeAccess(context, requiredScope: null);

        // Assert
        Assert.Null(error);
    }
}
