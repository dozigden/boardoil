using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace BoardOil.Api.Tests;

public abstract class BoardApiIntegrationTestBase : TestBaseIntegration
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected async Task SeedTagAsync(string name, string normalisedName, string styleName, string stylePropertiesJson)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "Tags" ("BoardId", "Name", "NormalisedName", "StyleName", "StylePropertiesJson", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ($boardId, $name, $normalisedName, $styleName, $stylePropertiesJson, $createdAtUtc, $updatedAtUtc);
            """;
        command.Parameters.AddWithValue("$boardId", 1);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$normalisedName", normalisedName);
        command.Parameters.AddWithValue("$styleName", styleName);
        command.Parameters.AddWithValue("$stylePropertiesJson", stylePropertiesJson);
        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("$createdAtUtc", now);
        command.Parameters.AddWithValue("$updatedAtUtc", now);
        await command.ExecuteNonQueryAsync();
    }

    protected async Task AssignCardTypeToCardAsync(int cardId, int cardTypeId)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE "Cards"
            SET "CardTypeId" = $cardTypeId
            WHERE "Id" = $cardId;
            """;
        command.Parameters.AddWithValue("$cardId", cardId);
        command.Parameters.AddWithValue("$cardTypeId", cardTypeId);
        await command.ExecuteNonQueryAsync();
    }

    protected async Task<int> GetCardTypeIdForCardAsync(int cardId)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "CardTypeId"
            FROM "Cards"
            WHERE "Id" = $cardId;
            """;
        command.Parameters.AddWithValue("$cardId", cardId);
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value);
    }

    protected sealed record ApiEnvelope<T>(
        bool Success,
        T? Data,
        int StatusCode,
        string? Message,
        Dictionary<string, string[]>? ValidationErrors = null);
}
