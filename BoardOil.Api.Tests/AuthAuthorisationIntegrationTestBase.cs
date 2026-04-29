using System.Net.Http.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Ordering;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Ef;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public abstract class AuthAuthorisationIntegrationTestBase : ApiFactoryIntegrationTestBase
{
    protected async Task SeedTagAsync(string name, string normalisedName, string styleName, string stylePropertiesJson)
    {
        await ArrangeAsync(async dbContext =>
        {
            var boardExists = await dbContext.Boards.AnyAsync(x => x.Id == 1);
            if (!boardExists)
            {
                throw new InvalidOperationException("Board with id 1 was not found.");
            }

            var now = DateTime.UtcNow;
            dbContext.Tags.Add(new EntityTag
            {
                BoardId = 1,
                Name = name,
                NormalisedName = normalisedName,
                StyleName = styleName,
                StylePropertiesJson = stylePropertiesJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        });
    }

    protected async Task<int> SeedBoardTagAsync(string name, int boardId = 1)
    {
        return await ArrangeAsync(async dbContext =>
        {
            var boardExists = await dbContext.Boards.AnyAsync(x => x.Id == boardId);
            if (!boardExists)
            {
                throw new InvalidOperationException($"Board with id {boardId} was not found.");
            }

            var canonicalName = name.Trim();
            var now = DateTime.UtcNow;
            var tag = new EntityTag
            {
                BoardId = boardId,
                Name = canonicalName,
                NormalisedName = canonicalName.ToUpperInvariant(),
                StyleName = TagStyleSchemaValidator.SolidStyleName,
                StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Tags.Add(tag);
            return tag.Id;
        });
    }

    protected async Task RegisterInitialAdminAsync(HttpClient client)
    {
        _ = await AuthenticateAsInitialAdminAsync(client);
    }

    protected static async Task<int> CreateUserAsAdminAsync(HttpClient adminClient, string userName, string password, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/users",
            new CreateUserRequest(userName, userName, $"{userName}@localhost", password, role));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardOil.Contracts.Users.ManagedUserDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!.Id;
    }

    protected static async Task CreateClientAccountAsAdminAsync(HttpClient adminClient, string userName, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/client-accounts",
            new CreateClientAccountRequest(userName, userName, $"{userName}@localhost", role));
        response.EnsureSuccessStatusCode();
    }

    protected static async Task AddBoardMemberAsAdminAsync(HttpClient adminClient, int boardId, int userId, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new AddBoardMemberRequest(userId, role));
        response.EnsureSuccessStatusCode();
    }

    protected async Task<int> SeedBoardColumnAsync(string title, int boardId = 1)
    {
        var column = await ArrangeAsync(async dbContext =>
        {
            var boardExists = await dbContext.Boards
                .AnyAsync(x => x.Id == boardId);
            if (!boardExists)
            {
                throw new InvalidOperationException($"Board with id {boardId} was not found.");
            }

            var previousSortKey = await dbContext.Columns
                .Where(x => x.BoardId == boardId)
                .OrderByDescending(x => x.SortKey)
                .Select(x => x.SortKey)
                .FirstOrDefaultAsync();

            var now = DateTime.UtcNow;
            var column = new EntityBoardColumn
            {
                BoardId = boardId,
                Title = title,
                SortKey = SortKeyGenerator.Between(previousSortKey, null),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Columns.Add(column);
            return column;
        });

        return column.Id;
    }

    protected async Task<int> SeedBoardCardAsync(int columnId, string title, string description)
    {
        return await UseDbContextAsync(async dbContext =>
        {
            var column = await dbContext.Columns.FindAsync(columnId);
            if (column is null)
            {
                throw new InvalidOperationException($"Column with id {columnId} was not found.");
            }

            var now = DateTime.UtcNow;
            var board = await dbContext.Boards.FindAsync(column.BoardId);
            if (board is null)
            {
                throw new InvalidOperationException($"Board with id {column.BoardId} was not found.");
            }

            var cardType = await dbContext.CardTypes
                .Where(x => x.BoardId == column.BoardId && x.IsSystem)
                .FirstOrDefaultAsync();

            if (cardType is null)
            {
                cardType = CardTypeDefaults.CreateSystemForBoard(board, now);
                dbContext.CardTypes.Add(cardType);
            }

            var previousSortKey = await dbContext.Cards
                .Where(x => x.BoardColumnId == columnId)
                .OrderByDescending(x => x.SortKey)
                .Select(x => x.SortKey)
                .FirstOrDefaultAsync();

            var card = new EntityBoardCard
            {
                BoardColumnId = columnId,
                CardType = cardType,
                Title = title,
                Description = description,
                SortKey = SortKeyGenerator.Between(previousSortKey, null),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            return card.Id;
        });
    }

    protected async Task<int> SeedBoardCardTypeAsync(string name, int boardId = 1, string? emoji = null, bool isSystem = false)
    {
        return await ArrangeAsync(async dbContext =>
        {
            var board = await dbContext.Boards.FindAsync(boardId);
            if (board is null)
            {
                throw new InvalidOperationException($"Board with id {boardId} was not found.");
            }

            var now = DateTime.UtcNow;
            var cardType = new EntityCardType
            {
                BoardId = boardId,
                Name = name.Trim(),
                Emoji = emoji,
                StyleName = CardTypeDefaults.DefaultStyleName,
                StylePropertiesJson = CardTypeDefaults.DefaultStylePropertiesJson,
                IsSystem = isSystem,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.CardTypes.Add(cardType);
            return cardType.Id;
        });
    }

    protected static async Task LoginAsAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);
    }

    protected sealed record LoginRequest(string UserName, string Password);
    protected sealed record CreateUserRequest(string UserName, string DisplayName, string Email, string Password, string Role);
    protected sealed record ResetUserPasswordRequest(string NewPassword);
    protected sealed record CreateClientAccountRequest(string UserName, string DisplayName, string Email, string Role);
    protected sealed record UpdateUserRequest(string Email, string Role, bool IsActive);
    protected sealed record UpdateClientAccountRequest(string Email, string Role, bool IsActive);
    protected sealed record UpdateConfigurationRequest(string? McpPublicBaseUrl);
    protected sealed record AuthSessionEnvelope(string CsrfToken);
    protected sealed record ConfigurationEnvelope(bool AllowInsecureCookies, string? McpPublicBaseUrl);
    protected sealed record UserDirectoryEntryEnvelope(int Id, string UserName, bool IsActive);
    protected sealed record ManagedUserEnvelope(int Id, string UserName, string Email, string Role, string IdentityType, bool IsActive);
    protected sealed record ClientAccountEnvelope(int Id, string UserName, string Email, string Role, bool IsActive);
    protected sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);

    private async Task<T> UseDbContextAsync<T>(Func<BoardOilDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        return await action(dbContext);
    }

    private async Task ArrangeAsync(Func<BoardOilDbContext, Task> arrange)
    {
        await UseDbContextAsync(async dbContext =>
        {
            await arrange(dbContext);
            await dbContext.SaveChangesAsync();
            return 0;
        });
    }

    private async Task<T> ArrangeAsync<T>(Func<BoardOilDbContext, Task<T>> arrange)
    {
        return await UseDbContextAsync(async dbContext =>
        {
            var result = await arrange(dbContext);
            await dbContext.SaveChangesAsync();
            return result;
        });
    }
}
