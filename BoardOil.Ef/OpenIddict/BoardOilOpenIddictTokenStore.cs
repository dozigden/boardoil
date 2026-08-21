using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace BoardOil.Ef.OpenIddict;

public sealed class BoardOilOpenIddictTokenStore(
    IMemoryCache cache,
    IOpenIddictEntityFrameworkCoreContext context,
    IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options)
    : OpenIddictEntityFrameworkCoreTokenStore(cache, context, options)
{
    public override async ValueTask UpdateAsync(
        OpenIddictEntityFrameworkCoreToken token,
        CancellationToken cancellationToken)
    {
        var dbContext = await Context.GetDbContextAsync(cancellationToken);

        try
        {
            await base.UpdateAsync(token, cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException { SqliteErrorCode: 5 })
        {
            // OpenIddict deliberately tolerates failed redemption writes for concurrent refresh-token
            // requests. Ensure SQLite lock contention cannot leave the stale update tracked
            // and contaminate token entries created later in the same request.
            dbContext.Entry(token).State = EntityState.Unchanged;
            throw;
        }
    }
}
