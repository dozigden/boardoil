using System.Globalization;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.OAuth;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Auth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Auth;

public sealed class AttachmentTransferCredentialValidator(
    IDbContextScopeFactory scopes, IPersonalAccessTokenRepository pats, IOAuthConnectionRepository connections,
    IOpenIddictTokenManager tokens, IOpenIddictAuthorizationManager authorizations,
    IOpenIddictApplicationManager applications, TimeProvider clock) : IAttachmentTransferCredentialValidator
{
    public async Task<ApiResult<DateTime?>> ValidateAsync(int actorUserId, AttachmentTransferCredential credential,
        string requiredScope, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateReadOnly();
        var now = clock.GetUtcNow().UtcDateTime;
        if (credential.PersonalAccessTokenId is { } patId)
        {
            if (credential.OAuthTokenId is not null || credential.OAuthAuthorizationId is not null) { return Invalid(); }
            var pat = await pats.Query().Include(x => x.User).SingleOrDefaultAsync(x => x.Id == patId, cancellationToken);
            if (pat is null || pat.UserId != actorUserId || !pat.User.IsActive || pat.RevokedAtUtc is not null ||
                pat.ExpiresAtUtc <= now || !HasScope(pat.ScopesCsv, requiredScope)) { return Invalid(); }
            return ApiResults.Ok(pat.ExpiresAtUtc);
        }

        if (string.IsNullOrWhiteSpace(credential.OAuthTokenId) || string.IsNullOrWhiteSpace(credential.OAuthAuthorizationId)) { return Invalid(); }
        var token = await tokens.FindByIdAsync(credential.OAuthTokenId, cancellationToken);
        var subject = actorUserId.ToString(CultureInfo.InvariantCulture);
        if (token is null || !await tokens.HasStatusAsync(token, Statuses.Valid, cancellationToken) ||
            !await tokens.HasTypeAsync(token, TokenTypeIdentifiers.AccessToken, cancellationToken) ||
            await tokens.GetSubjectAsync(token, cancellationToken) != subject ||
            await tokens.GetAuthorizationIdAsync(token, cancellationToken) != credential.OAuthAuthorizationId) { return Invalid(); }
        var expires = await tokens.GetExpirationDateAsync(token, cancellationToken);
        if (expires is null || expires.Value.UtcDateTime <= now) { return Invalid(); }

        var authorization = await authorizations.FindByIdAsync(credential.OAuthAuthorizationId, cancellationToken);
        if (authorization is null || !await authorizations.HasStatusAsync(authorization, Statuses.Valid, cancellationToken) ||
            await authorizations.GetSubjectAsync(authorization, cancellationToken) != subject ||
            !(await authorizations.GetScopesAsync(authorization, cancellationToken)).Contains(requiredScope)) { return Invalid(); }
        var applicationId = await authorizations.GetApplicationIdAsync(authorization, cancellationToken);
        if (string.IsNullOrEmpty(applicationId) || await tokens.GetApplicationIdAsync(token, cancellationToken) != applicationId) { return Invalid(); }
        var application = await applications.FindByIdAsync(applicationId, cancellationToken);
        if (application is null || !await OAuthApplicationValidity.IsActiveAsync(applications, application, clock, cancellationToken)) { return Invalid(); }
        var clientId = await applications.GetClientIdAsync(application, cancellationToken);
        var grant = await connections.GetGrantByAuthorizationIdAsync(credential.OAuthAuthorizationId);
        if (grant is null || grant.OpenIddictApplicationId != applicationId || grant.OAuthClientId != clientId ||
            grant.RevokedAtUtc is not null || !HasScope(grant.ApprovedScopesCsv, requiredScope) ||
            grant.OAuthConnection.UserId != actorUserId || grant.OAuthConnection.ActiveGrantId != grant.Id ||
            grant.OAuthConnection.RevokedAtUtc is not null || !grant.OAuthConnection.User.IsActive ||
            grant.OAuthConnection.User.IdentityType != UserIdentityType.User) { return Invalid(); }
        return ApiResults.Ok<DateTime?>(expires.Value.UtcDateTime);
    }

    private static bool HasScope(string scopes, string requiredScope) =>
        scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(requiredScope, StringComparer.Ordinal);
    private static ApiError Invalid() => ApiErrors.Unauthorized("The originating credential is no longer valid.");
}
