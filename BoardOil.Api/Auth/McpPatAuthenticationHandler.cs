using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Mcp;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BoardOil.Api.Auth;

public sealed class McpPatAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory,
    IPersonalAccessTokenRepository personalAccessTokenRepository)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;
    private readonly IPersonalAccessTokenRepository _personalAccessTokenRepository = personalAccessTokenRepository;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        if (!TryGetBearerToken(out var token))
        {
            return AuthenticateResult.NoResult();
        }

        if (!token.StartsWith("bo_pat_", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        using var scope = _scopeFactory.Create();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = HashToken(token);
        var personalAccessToken = await _personalAccessTokenRepository.GetWithUserByHashAsync(tokenHash);
        if (personalAccessToken is null
            || personalAccessToken.RevokedAtUtc is not null
            || (personalAccessToken.ExpiresAtUtc is not null && personalAccessToken.ExpiresAtUtc <= now)
            || !personalAccessToken.User.IsActive)
        {
            return AuthenticateResult.Fail("Invalid or expired bearer token.");
        }

        var scopes = ParseScopes(personalAccessToken.ScopesCsv);
        if (!HasAnyMcpScope(scopes))
        {
            return AuthenticateResult.Fail("Personal access token does not allow MCP access.");
        }

        personalAccessToken.LastUsedAtUtc = now;
        await scope.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, personalAccessToken.UserId.ToString()),
            new(ClaimTypes.Name, personalAccessToken.User.UserName),
            new(ClaimTypes.Role, personalAccessToken.User.Role.ToString()),
            new("boardoil_auth_type", "pat"),
            new("boardoil_pat_id", personalAccessToken.Id.ToString()),
            new("boardoil_pat_name", personalAccessToken.Name),
            new("boardoil_pat_board_access_mode", NormaliseBoardAccessMode(personalAccessToken.BoardAccessMode)),
            new("boardoil_pat_allowed_board_ids", personalAccessToken.AllowedBoardIdsCsv ?? string.Empty)
        };

        claims.AddRange(scopes.Select(scopeName => new Claim("boardoil_pat_scope", scopeName)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (!Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await base.HandleChallengeAsync(properties);
            return;
        }

        if (Response.HasStarted)
        {
            return;
        }

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer realm=\"BoardOil MCP\"";
        await Response.WriteAsJsonAsync(CreateMcpAuthError("Invalid or expired bearer token."));
    }

    private bool TryGetBearerToken(out string token)
    {
        token = string.Empty;
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader)
            || !authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = authHeader[BearerPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    private object CreateMcpAuthError(string detail) =>
        new ApiResult<object>(
            false,
            new
            {
                auth = McpDiscoveryMetadata.CreateAuthMetadata(GetBaseUrl()),
                endpoint = $"{GetBaseUrl()}/mcp",
                docs = $"{GetBaseUrl()}/.well-known/mcp",
                setup = McpDiscoveryMetadata.CreateSetupMetadata(GetBaseUrl()),
                examples = McpDiscoveryMetadata.CreateExamples(GetBaseUrl()),
                nextStep = "Create a PAT in the machine access UI, then call POST /mcp with Authorization: Bearer <YOUR_PAT>."
            },
            401,
            detail);

    private string GetBaseUrl() =>
        $"{Request.Scheme}://{Request.Host}";

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static IReadOnlyList<string> ParseScopes(string scopesCsv)
    {
        var rawScopes = scopesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var hasLegacyMcp = rawScopes.Contains(MachinePatScopes.LegacyMcp, StringComparer.Ordinal);
        var scopes = new List<string>();
        if (hasLegacyMcp || rawScopes.Contains(MachinePatScopes.McpRead, StringComparer.Ordinal))
        {
            scopes.Add(MachinePatScopes.McpRead);
        }

        if (hasLegacyMcp || rawScopes.Contains(MachinePatScopes.McpWrite, StringComparer.Ordinal))
        {
            scopes.Add(MachinePatScopes.McpWrite);
        }

        return scopes;
    }

    private static bool HasAnyMcpScope(IEnumerable<string> scopes) =>
        scopes.Contains(MachinePatScopes.McpRead, StringComparer.Ordinal)
        || scopes.Contains(MachinePatScopes.McpWrite, StringComparer.Ordinal);

    private static string NormaliseBoardAccessMode(string? boardAccessMode)
    {
        if (string.IsNullOrWhiteSpace(boardAccessMode))
        {
            return MachinePatBoardAccessModes.All;
        }

        return boardAccessMode.Trim().ToLowerInvariant();
    }
}
