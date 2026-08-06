using System.Security.Cryptography;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Mcp;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Mcp;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Mcp;
using BoardOil.Data.Abstractions.Users;

namespace BoardOil.Services.Mcp;

public sealed class McpProjectConnectionService(
    IMcpProjectConnectionRepository connectionRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory) : IMcpProjectConnectionService
{
    private static readonly string[] SupportedScopes =
    [
        MachinePatScopes.McpRead,
        MachinePatScopes.McpWrite
    ];

    public async Task<ApiResult<IReadOnlyList<McpProjectConnectionDto>>> GetConnectionsAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var connections = await connectionRepository.GetAllOrderedAsync();
        return connections.Select(ToDto).ToArray();
    }

    public async Task<ApiResult<McpProjectConnectionDto>> CreateConnectionAsync(
        int actorUserId,
        CreateMcpProjectConnectionRequest request)
    {
        using var scope = scopeFactory.Create();

        var name = request.Name?.Trim() ?? string.Empty;
        var requestedScopes = NormaliseRequestedScopes(request.AllowedScopes);
        var validationErrors = ValidateCreateRequest(request.ClientAccountId, name, requestedScopes);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        var actorResult = GetActiveAdminActor(actorUserId);
        if (!actorResult.Success || actorResult.Data is null)
        {
            return new ApiResult<McpProjectConnectionDto>(
                false,
                null,
                actorResult.StatusCode,
                actorResult.Message,
                actorResult.ValidationErrors);
        }

        var clientAccount = userRepository.Get(request.ClientAccountId);
        if (clientAccount is null || clientAccount.IdentityType != UserIdentityType.Client)
        {
            return ApiErrors.NotFound("Client account not found.");
        }

        var allowedScopes = SupportedScopes
            .Where(requestedScopes.Contains)
            .ToArray();
        var publicId = await CreateUniquePublicIdAsync();
        var connection = new EntityMcpProjectConnection
        {
            PublicId = publicId,
            Name = name,
            ClientAccountId = clientAccount.Id,
            ClientAccount = clientAccount,
            AllowedScopesCsv = string.Join(',', allowedScopes),
            CreatedByUserId = actorResult.Data.Id,
            CreatedByUser = actorResult.Data,
            CreatedByUserName = actorResult.Data.UserName,
        };

        connectionRepository.Add(connection);
        await scope.SaveChangesAsync();

        return ApiResults.Created(ToDto(connection));
    }

    public async Task<ApiResult> RevokeConnectionAsync(int actorUserId, int connectionId)
    {
        using var scope = scopeFactory.Create();

        var actorResult = GetActiveAdminActor(actorUserId);
        if (!actorResult.Success || actorResult.Data is null)
        {
            return new ApiResult(
                false,
                actorResult.StatusCode,
                actorResult.Message,
                actorResult.ValidationErrors);
        }

        var connection = await connectionRepository.GetByIdWithClientAccountAsync(connectionId);
        if (connection is null)
        {
            return ApiErrors.NotFound("Project connection not found.");
        }

        if (connection.RevokedAtUtc is not null)
        {
            return ApiResults.Ok();
        }

        connection.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        connection.RevokedByUserId = actorResult.Data.Id;
        connection.RevokedByUser = actorResult.Data;
        connection.RevokedByUserName = actorResult.Data.UserName;
        await scope.SaveChangesAsync();

        return ApiResults.Ok();
    }

    private ApiResult<EntityUser> GetActiveAdminActor(int actorUserId)
    {
        var actor = userRepository.Get(actorUserId);
        if (actor is null || !actor.IsActive || actor.IdentityType != UserIdentityType.User)
        {
            return ApiErrors.Unauthorized("Administrator identity is not active.");
        }

        if (actor.Role != UserRole.Admin)
        {
            return ApiErrors.Forbidden("Administrator access is required.");
        }

        return ApiResults.Ok(actor);
    }

    private async Task<string> CreateUniquePublicIdAsync()
    {
        while (true)
        {
            var publicId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            if (!await connectionRepository.PublicIdExistsAsync(publicId))
            {
                return publicId;
            }
        }
    }

    private static string[] NormaliseRequestedScopes(IEnumerable<string>? scopes)
    {
        var requested = (scopes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        return requested.ToArray();
    }

    private static IReadOnlyList<ValidationError> ValidateCreateRequest(
        int clientAccountId,
        string name,
        IReadOnlyList<string> normalisedScopes)
    {
        var errors = new List<ValidationError>();
        if (clientAccountId <= 0)
        {
            errors.Add(new ValidationError("clientAccountId", "Client account is required."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new ValidationError("name", "Connection name is required."));
        }
        else if (name.Length > 120)
        {
            errors.Add(new ValidationError("name", "Connection name must be 120 characters or fewer."));
        }

        if (normalisedScopes.Count == 0)
        {
            errors.Add(new ValidationError("allowedScopes", "Select at least one supported MCP scope."));
        }
        else if (normalisedScopes.Except(SupportedScopes, StringComparer.Ordinal).Any())
        {
            errors.Add(new ValidationError("allowedScopes", "Only mcp:read and mcp:write are supported."));
        }

        return errors;
    }

    private static McpProjectConnectionDto ToDto(EntityMcpProjectConnection connection) =>
        new(
            connection.Id,
            connection.PublicId,
            connection.Name,
            connection.ClientAccountId,
            connection.ClientAccount.UserName,
            connection.ClientAccount.DisplayName,
            connection.AllowedScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            $"/mcp/connections/{connection.PublicId}",
            connection.RevokedAtUtc is null,
            connection.CreatedByUserId,
            connection.CreatedByUserName,
            connection.CreatedAtUtc,
            connection.UpdatedAtUtc,
            connection.RevokedAtUtc,
            connection.RevokedByUserId,
            connection.RevokedByUserName);
}
