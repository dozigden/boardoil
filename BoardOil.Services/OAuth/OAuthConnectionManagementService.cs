using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.OAuth;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
using BoardOil.Data.Abstractions.Users;

namespace BoardOil.Services.OAuth;

public sealed class OAuthConnectionManagementService(
    IOAuthConnectionRepository connectionRepository,
    IUserRepository userRepository,
    IOAuthAuthorizationRevoker authorizationRevoker,
    IDbContextScopeFactory scopeFactory) : IOAuthConnectionManagementService
{
    public async Task<ApiResult<IReadOnlyList<OAuthConnectionDto>>> GetOwnConnectionsAsync(int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var actor = userRepository.Get(actorUserId);
        var actorError = ValidateOwnerActor(actor);
        if (actorError is not null)
        {
            return actorError;
        }

        var connections = await connectionRepository.GetActiveForUserAsync(actorUserId);
        return connections.Select(ToDto).ToArray();
    }

    public async Task<ApiResult<IReadOnlyList<OAuthConnectionDto>>> GetAllConnectionsAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();
        var connections = await connectionRepository.GetAllActiveAsync();
        return connections.Select(ToDto).ToArray();
    }

    public Task<ApiResult> RevokeOwnConnectionAsync(
        int connectionId,
        int actorUserId,
        CancellationToken cancellationToken = default) =>
        RevokeConnectionAsync(connectionId, actorUserId, ownerOnly: true, cancellationToken);

    public Task<ApiResult> RevokeConnectionAsAdminAsync(
        int connectionId,
        int actorUserId,
        CancellationToken cancellationToken = default) =>
        RevokeConnectionAsync(connectionId, actorUserId, ownerOnly: false, cancellationToken);

    private async Task<ApiResult> RevokeConnectionAsync(
        int connectionId,
        int actorUserId,
        bool ownerOnly,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.Create();

        var actor = userRepository.Get(actorUserId);
        if (actor is null || !actor.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        if (ownerOnly && actor.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Forbidden("Client accounts do not own OAuth connections.");
        }

        var connection = await connectionRepository.GetByIdWithActiveGrantAsync(connectionId);
        if (connection is null || (ownerOnly && connection.UserId != actorUserId))
        {
            return ApiErrors.NotFound("OAuth connection not found.");
        }

        var authorizationId = connection.ActiveGrant?.OpenIddictAuthorizationId;
        if (!string.IsNullOrWhiteSpace(authorizationId))
        {
            await authorizationRevoker.RevokeAsync(authorizationId, cancellationToken);
        }

        await scope.Transaction(async (transactionScope, transaction) =>
        {
            connection.ActiveGrant = null;
            await transactionScope.SaveChangesAsync(cancellationToken);
            connectionRepository.Remove(connection);
            await transactionScope.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync();
        });

        return ApiResults.Ok();
    }

    private static ApiError? ValidateOwnerActor(EntityUser? actor)
    {
        if (actor is null || !actor.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        if (actor.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Forbidden("Client accounts do not own OAuth connections.");
        }

        return null;
    }

    private static OAuthConnectionDto ToDto(EntityOAuthConnection connection)
    {
        var activeGrant = connection.ActiveGrant
            ?? throw new InvalidOperationException("An active OAuth connection must have an active grant.");

        return new OAuthConnectionDto(
            connection.Id,
            connection.Name,
            connection.ResourceType,
            new OAuthConnectionOwnerDto(
                connection.User.Id,
                connection.User.UserName,
                connection.User.DisplayName),
            ParseScopes(activeGrant.ApprovedScopesCsv),
            activeGrant.OAuthClientId,
            activeGrant.OAuthClientDisplayName,
            activeGrant.Resource,
            connection.CreatedAtUtc,
            activeGrant.ApprovedAtUtc);
    }

    private static IReadOnlyList<string> ParseScopes(string scopesCsv) =>
        scopesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
