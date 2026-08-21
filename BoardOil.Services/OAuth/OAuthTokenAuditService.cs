using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.OAuth;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
using Microsoft.Extensions.Logging;

namespace BoardOil.Services.OAuth;

public sealed class OAuthTokenAuditService(
    IDbContextScopeFactory scopeFactory,
    IOAuthTokenAuditRepository auditRepository,
    IOAuthConnectionRepository connectionRepository,
    TimeProvider timeProvider,
    ILogger<OAuthTokenAuditService> logger) : IOAuthTokenAuditService
{
    private const int DefaultPageSize = 100;
    private const int MaxPageSize = 200;

    public async Task<ApiResult<OAuthTokenAuditListDto>> ListAsync(
        int? offset,
        int? limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? outcome,
        string? grantType,
        int? connectionId,
        string? clientId,
        string? authorizationId,
        string? tokenFingerprint)
    {
        var listOffset = offset ?? 0;
        var listLimit = limit ?? DefaultPageSize;
        var normalisedOutcome = NormaliseOutcome(outcome);
        var errors = ValidateQuery(
            listOffset,
            listLimit,
            fromUtc,
            toUtc,
            outcome,
            normalisedOutcome,
            connectionId);
        if (errors.Count > 0)
        {
            return ApiErrors.BadRequest("Invalid OAuth token audit query.", errors);
        }

        var query = new OAuthTokenAuditQuery(
            fromUtc,
            toUtc,
            normalisedOutcome,
            NormaliseOptional(grantType),
            connectionId,
            NormaliseOptional(clientId),
            NormaliseOptional(authorizationId),
            NormaliseOptional(tokenFingerprint));
        using var scope = scopeFactory.CreateReadOnly();
        var totalCount = await auditRepository.CountAsync(query);
        var audits = await auditRepository.ListAsync(query, listOffset, listLimit);
        return ApiResults.Ok(new OAuthTokenAuditListDto(
            audits.Select(ToDto).ToArray(),
            listOffset,
            listLimit,
            totalCount));
    }

    public async Task<ApiResult<OAuthTokenAuditPurgeResultDto>> PurgeExpiredAsync(
        CancellationToken cancellationToken = default)
    {
        var cutoffUtc = timeProvider
            .GetUtcNow()
            .UtcDateTime
            .AddDays(-OAuthTokenAuditRetention.RetentionDays);
        using var scope = scopeFactory.Create();
        var deletedCount = await auditRepository.DeleteOlderThanAsync(cutoffUtc, cancellationToken);
        return ApiResults.Ok(new OAuthTokenAuditPurgeResultDto(
            OAuthTokenAuditRetention.RetentionDays,
            cutoffUtc,
            deletedCount));
    }

    public async Task RecordAsync(OAuthTokenAuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            using var _ = scopeFactory.SuppressAmbientContext();
            using var scope = scopeFactory.Create(DbContextScopeOption.ForceCreateNew);
            var grant = string.IsNullOrWhiteSpace(input.AuthorizationId)
                ? null
                : await connectionRepository.GetGrantByAuthorizationIdAsync(input.AuthorizationId);
            var entity = new EntityOAuthTokenAudit
            {
                OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                Outcome = TruncateRequired(input.Outcome, 16),
                GrantType = TruncateRequired(input.GrantType, 32),
                RequestedScopes = NormaliseRequestedScopes(input.RequestedScopes),
                ErrorCode = Truncate(input.ErrorCode, 64),
                ErrorDescription = SanitiseDiagnosticText(input.ErrorDescription, 512),
                ErrorUri = SanitiseDiagnosticText(input.ErrorUri, 512),
                PresentedTokenFingerprint = Truncate(input.PresentedTokenFingerprint, 71),
                IssuedRefreshTokenFingerprint = Truncate(input.IssuedRefreshTokenFingerprint, 71),
                AuthorizationId = Truncate(input.AuthorizationId, 100),
                OAuthClientId = Truncate(input.OAuthClientId ?? grant?.OAuthClientId, 100),
                OAuthConnectionId = grant?.OAuthConnectionId,
                OAuthConnectionName = Truncate(grant?.OAuthConnection.Name, 120),
                OwnerUserId = grant?.OAuthConnection.UserId,
                OwnerUserName = Truncate(grant?.OAuthConnection.User.UserName, 64),
                OAuthClientDisplayName = Truncate(grant?.OAuthClientDisplayName, 200),
                Resource = Truncate(grant?.Resource, 2048),
                TraceIdentifier = Truncate(input.TraceIdentifier, 128),
                UserAgent = SanitiseDiagnosticText(input.UserAgent, 512)
            };

            auditRepository.Add(entity);
            await scope.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to persist an OAuth token audit event.");
        }
    }

    private static List<ValidationError> ValidateQuery(
        int offset,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? suppliedOutcome,
        string? normalisedOutcome,
        int? connectionId)
    {
        var errors = new List<ValidationError>();
        if (offset < 0)
        {
            errors.Add(new ValidationError(nameof(offset), "Offset must be 0 or greater."));
        }

        if (limit < 1 || limit > MaxPageSize)
        {
            errors.Add(new ValidationError(nameof(limit), $"Limit must be between 1 and {MaxPageSize}."));
        }

        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
        {
            errors.Add(new ValidationError(nameof(fromUtc), "FromUtc must be before or equal to ToUtc."));
        }

        if (!string.IsNullOrWhiteSpace(suppliedOutcome) && normalisedOutcome is null)
        {
            errors.Add(new ValidationError(nameof(suppliedOutcome), "Outcome must be Succeeded or Rejected."));
        }

        if (connectionId is <= 0)
        {
            errors.Add(new ValidationError(nameof(connectionId), "ConnectionId must be greater than 0."));
        }

        return errors;
    }

    private static string? NormaliseOutcome(string? outcome)
    {
        if (string.Equals(outcome?.Trim(), OAuthTokenAuditOutcomes.Succeeded, StringComparison.OrdinalIgnoreCase))
        {
            return OAuthTokenAuditOutcomes.Succeeded;
        }

        if (string.Equals(outcome?.Trim(), OAuthTokenAuditOutcomes.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return OAuthTokenAuditOutcomes.Rejected;
        }

        return null;
    }

    private static string? NormaliseOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormaliseRequestedScopes(IReadOnlyCollection<string> requestedScopes)
    {
        if (requestedScopes.Count == 0)
        {
            return null;
        }

        var recognisedScopes = new List<string>(2);
        if (requestedScopes.Contains(MachinePatScopes.McpRead, StringComparer.Ordinal))
        {
            recognisedScopes.Add(MachinePatScopes.McpRead);
        }

        if (requestedScopes.Contains(MachinePatScopes.McpWrite, StringComparer.Ordinal))
        {
            recognisedScopes.Add(MachinePatScopes.McpWrite);
        }

        return recognisedScopes.Count == 0
            ? null
            : string.Join(' ', recognisedScopes);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        var normalised = NormaliseOptional(value);
        if (normalised is null || normalised.Length <= maxLength)
        {
            return normalised;
        }

        return normalised[..maxLength];
    }

    private static string TruncateRequired(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? SanitiseDiagnosticText(string? value, int maxLength)
    {
        var truncated = Truncate(value, maxLength);
        if (truncated is null)
        {
            return null;
        }

        var characters = truncated.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (char.IsControl(characters[index]))
            {
                characters[index] = ' ';
            }
        }

        return new string(characters);
    }

    private static OAuthTokenAuditDto ToDto(EntityOAuthTokenAudit audit) =>
        new(
            audit.Id,
            audit.OccurredAtUtc,
            audit.Outcome,
            audit.GrantType,
            audit.RequestedScopes,
            audit.ErrorCode,
            audit.ErrorDescription,
            audit.ErrorUri,
            audit.PresentedTokenFingerprint,
            audit.IssuedRefreshTokenFingerprint,
            audit.AuthorizationId,
            audit.OAuthClientId,
            audit.OAuthConnectionId,
            audit.OAuthConnectionName,
            audit.OwnerUserId,
            audit.OwnerUserName,
            audit.OAuthClientDisplayName,
            audit.Resource,
            audit.TraceIdentifier,
            audit.UserAgent);
}
