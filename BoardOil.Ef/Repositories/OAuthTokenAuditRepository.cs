using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class OAuthTokenAuditRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityOAuthTokenAudit>(ambientDbContextLocator), IOAuthTokenAuditRepository
{
    public Task<int> CountAsync(OAuthTokenAuditQuery query) =>
        ApplyQuery(query).CountAsync();

    public async Task<IReadOnlyList<EntityOAuthTokenAudit>> ListAsync(
        OAuthTokenAuditQuery query,
        int offset,
        int limit) =>
        await ApplyQuery(query)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        DbSet
            .Where(x => x.OccurredAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

    private IQueryable<EntityOAuthTokenAudit> ApplyQuery(OAuthTokenAuditQuery query)
    {
        var audits = DbSet.AsNoTracking();
        if (query.FromUtc is { } fromUtc)
        {
            audits = audits.Where(x => x.OccurredAtUtc >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            audits = audits.Where(x => x.OccurredAtUtc <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            audits = audits.Where(x => x.Outcome == query.Outcome);
        }

        if (!string.IsNullOrWhiteSpace(query.GrantType))
        {
            audits = audits.Where(x => x.GrantType == query.GrantType);
        }

        if (query.OAuthConnectionId is { } connectionId)
        {
            audits = audits.Where(x => x.OAuthConnectionId == connectionId);
        }

        if (!string.IsNullOrWhiteSpace(query.OAuthClientId))
        {
            audits = audits.Where(x => x.OAuthClientId == query.OAuthClientId);
        }

        if (!string.IsNullOrWhiteSpace(query.AuthorizationId))
        {
            audits = audits.Where(x => x.AuthorizationId == query.AuthorizationId);
        }

        if (!string.IsNullOrWhiteSpace(query.TokenFingerprint))
        {
            audits = audits.Where(x =>
                x.PresentedTokenFingerprint == query.TokenFingerprint
                || x.IssuedRefreshTokenFingerprint == query.TokenFingerprint);
        }

        return audits;
    }
}
