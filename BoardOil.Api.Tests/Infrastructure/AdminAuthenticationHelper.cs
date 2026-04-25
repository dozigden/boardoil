using System.Net.Http.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Auth;
using BoardOil.Ef;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests.Infrastructure;

internal static class AdminAuthenticationHelper
{
    private const string AdminUserName = "admin";
    private const string AdminEmail = "admin@localhost";
    private const string AdminPassword = "Password1234!";
    private const string AccessCookieName = "boardoil_access";
    private const string CsrfHeaderName = "X-BoardOil-CSRF";

    public static async Task<string> AuthenticateAsSeededAdminAsync(HttpClient client, IServiceProvider serviceProvider)
    {
        await EnsureAdminSeededAsync(serviceProvider);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUserName, AdminPassword));
        login.EnsureSuccessStatusCode();

        var loginEnvelope = await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(loginEnvelope);
        Assert.NotNull(loginEnvelope!.Data);
        SetCsrfHeader(client, loginEnvelope.Data!.CsrfToken);
        return TryGetCookieValue(login, AccessCookieName) ?? string.Empty;
    }

    public static async Task EnsureAdminSeededAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        var passwordHashService = scope.ServiceProvider.GetRequiredService<IPasswordHashService>();
        var now = DateTime.UtcNow;

        var adminUser = await dbContext.Users
            .SingleOrDefaultAsync(x => x.UserName == AdminUserName && x.IdentityType == UserIdentityType.User);

        if (adminUser is null)
        {
            adminUser = new EntityUser
            {
                UserName = AdminUserName,
                Email = AdminEmail,
                NormalisedEmail = AdminEmail,
                PasswordHash = passwordHashService.HashPassword(AdminPassword),
                Role = UserRole.Admin,
                IdentityType = UserIdentityType.User,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.Users.Add(adminUser);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            adminUser.Email = AdminEmail;
            adminUser.NormalisedEmail = AdminEmail;
            adminUser.PasswordHash = passwordHashService.HashPassword(AdminPassword);
            adminUser.Role = UserRole.Admin;
            adminUser.IdentityType = UserIdentityType.User;
            adminUser.IsActive = true;
            adminUser.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync();
        }

        var boardIds = await dbContext.Boards
            .Select(x => x.Id)
            .ToArrayAsync();
        var existingMembershipBoardIds = await dbContext.BoardMembers
            .Where(x => x.UserId == adminUser.Id)
            .Select(x => x.BoardId)
            .ToArrayAsync();
        var existingMembershipSet = existingMembershipBoardIds.ToHashSet();

        var missingMemberships = boardIds
            .Where(boardId => !existingMembershipSet.Contains(boardId))
            .Select(boardId => new EntityBoardMember
            {
                BoardId = boardId,
                UserId = adminUser.Id,
                Role = BoardMemberRole.Owner,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            })
            .ToArray();
        if (missingMemberships.Length > 0)
        {
            dbContext.BoardMembers.AddRange(missingMemberships);
            await dbContext.SaveChangesAsync();
        }
    }

    private static void SetCsrfHeader(HttpClient client, string csrfToken)
    {
        client.DefaultRequestHeaders.Remove(CsrfHeaderName);
        client.DefaultRequestHeaders.Add(CsrfHeaderName, csrfToken);
    }

    private static string? TryGetCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var prefix = $"{cookieName}=";
        foreach (var value in values)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var end = value.IndexOf(';');
            var token = end >= 0 ? value[prefix.Length..end] : value[prefix.Length..];
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
