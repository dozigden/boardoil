using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using System.Text.Json;

namespace BoardOil.Services.Card;

public static class ArchivedCardSnapshotSerialiser
{
    public const string SchemaName = "archived-card";
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string CreateSnapshotJson(int boardId, EntityBoardCard card, DateTime capturedAtUtc)
    {
        var payload = new ArchivedCardSnapshotV1Payload(
            boardId,
            card.RequireBoardCardId(),
            card.BoardColumnId,
            card.BoardColumn.Title,
            card.CardTypeId,
            card.CardType.Name,
            card.CardType.Emoji,
            card.Title,
            card.Description,
            card.SortKey,
            card.CardTags
                .Select(x => new CardTagDto(x.Tag.Id, x.Tag.Name, x.Tag.StyleName, x.Tag.StylePropertiesJson, x.Tag.Emoji))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ToList(),
            card.CardTags
                .Select(x => x.Tag.Name)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList(),
            card.CreatedAtUtc,
            card.UpdatedAtUtc,
            card.AssignedUserId,
            card.Comments
                .OrderBy(x => x.PostedAtUtc)
                .ThenBy(x => x.Id)
                .Select(x => new ArchivedCardSnapshotCommentV1Payload(
                    x.Text,
                    x.PostedAtUtc,
                    x.AuthorUserId,
                    x.AuthorUser?.Email))
                .ToList(),
            null,
            card.Slick?.Name,
            card.ExternalUrl);

        var envelope = new ArchivedCardSnapshotEnvelopeV1(
            SchemaName,
            CurrentVersion,
            capturedAtUtc,
            payload);
        return JsonSerializer.Serialize(envelope, SerializerOptions);
    }

    public static bool TryReadKnownPayload(string snapshotJson, out ArchivedCardSnapshotKnownPayload? knownPayload, out string? error)
    {
        knownPayload = null;
        error = null;
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            error = "Snapshot JSON is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("schema", out var schemaElement))
            {
                error = "Snapshot schema is missing.";
                return false;
            }

            var schema = schemaElement.GetString();
            if (!string.Equals(schema, SchemaName, StringComparison.Ordinal))
            {
                error = "Snapshot schema is not supported.";
                return false;
            }

            if (!root.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.Number)
            {
                error = "Snapshot version is missing.";
                return false;
            }

            var version = versionElement.GetInt32();
            if (version > CurrentVersion)
            {
                error = "Snapshot version is newer than this runtime supports.";
                return false;
            }

            if (version < 1)
            {
                error = "Snapshot version is invalid.";
                return false;
            }

            if (!root.TryGetProperty("capturedAtUtc", out var capturedAtElement))
            {
                error = "Snapshot capture time is missing.";
                return false;
            }

            var capturedAtUtc = capturedAtElement.GetDateTime();
            if (!root.TryGetProperty("payload", out var payloadElement))
            {
                error = "Snapshot payload is missing.";
                return false;
            }

            if (version == 1)
            {
                var payload = payloadElement.Deserialize<ArchivedCardSnapshotV1Payload>(SerializerOptions);
                if (payload is null)
                {
                    error = "Snapshot payload could not be parsed.";
                    return false;
                }

                knownPayload = new ArchivedCardSnapshotKnownPayload(schema!, version, capturedAtUtc, payload);
                return true;
            }

            error = "Snapshot version is not supported.";
            return false;
        }
        catch (JsonException)
        {
            error = "Snapshot JSON is invalid.";
            return false;
        }
    }

    public static bool TryBuildCurrentCardDto(string snapshotJson, out CardDto? card, out string? error)
    {
        card = null;
        var parsed = TryBuildCurrentSnapshot(snapshotJson, out var snapshot, out error);
        if (!parsed || snapshot is null)
        {
            return false;
        }

        card = snapshot.Card;
        return true;
    }

    public static bool TryBuildCurrentSnapshot(
        string snapshotJson,
        out ArchivedCardSnapshotCurrentDto? snapshot,
        out string? error)
    {
        snapshot = null;
        var parsed = TryReadKnownPayload(snapshotJson, out var knownPayload, out error);
        if (!parsed || knownPayload is null)
        {
            return false;
        }

        var payload = knownPayload.Payload;
        var card = new CardDto(
            payload.OriginalCardId,
            payload.BoardColumnId,
            payload.CardTypeId,
            payload.CardTypeName,
            payload.CardTypeEmoji,
            payload.Title,
            payload.Description,
            payload.SortKey,
            payload.Tags,
            payload.TagNames,
            payload.CreatedAtUtc,
            payload.UpdatedAtUtc,
            payload.AssignedUserId,
            null,
            null,
            payload.SlickId,
            payload.SlickName,
            payload.ExternalUrl);

        snapshot = new ArchivedCardSnapshotCurrentDto(
            card,
            payload.Comments?.ToList() ?? []);
        return true;
    }

}

public sealed record ArchivedCardSnapshotEnvelopeV1(
    string Schema,
    int Version,
    DateTime CapturedAtUtc,
    ArchivedCardSnapshotV1Payload Payload);

public sealed record ArchivedCardSnapshotV1Payload(
    int BoardId,
    int OriginalCardId,
    int BoardColumnId,
    string OriginalColumnName,
    int CardTypeId,
    string CardTypeName,
    string? CardTypeEmoji,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<CardTagDto> Tags,
    IReadOnlyList<string> TagNames,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int? AssignedUserId = null,
    IReadOnlyList<ArchivedCardSnapshotCommentV1Payload>? Comments = null,
    int? SlickId = null,
    string? SlickName = null,
    string? ExternalUrl = null);

public sealed record ArchivedCardSnapshotCommentV1Payload(
    string Text,
    DateTime CreatedAtUtc,
    int? AuthorUserId = null,
    string? AuthorEmail = null);

public sealed record ArchivedCardSnapshotKnownPayload(
    string Schema,
    int Version,
    DateTime CapturedAtUtc,
    ArchivedCardSnapshotV1Payload Payload);

public sealed record ArchivedCardSnapshotCurrentDto(
    CardDto Card,
    IReadOnlyList<ArchivedCardSnapshotCommentV1Payload> Comments);
