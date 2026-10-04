using System.Text.Json;
using System.Text.Json.Nodes;
using BoardOil.Contracts.Common;
using BoardOil.Services.CardField;

namespace BoardOil.Services.Board.Import;

internal static class BoardPackageArchiveFields
{
    public static CardFieldRuleResult<string> Normalise(string snapshotJson, string path)
    {
        using var document = ReadFieldSnapshot(snapshotJson);
        if (document is null) { return new(snapshotJson, []); }
        var payload = document.RootElement.GetProperty("payload");
        try
        {
            var definitions = CardFieldJsonCodec.DeserialiseDefinitions(
                payload.TryGetProperty("fieldDefinitions", out var fields) ? fields.GetRawText() : "[]");
            var values = CardFieldJsonCodec.DeserialiseValues(
                payload.TryGetProperty("fields", out var data) ? data.GetRawText() : "{}");
            var normalisedDefinitions = CardFieldRules.NormaliseDefinitions(definitions);
            if (!normalisedDefinitions.IsValid)
            {
                return new(null, normalisedDefinitions.Errors.Select(error =>
                    new ValidationError($"{path}.{error.Property}", error.Message)).ToArray());
            }
            var result = CardFieldRules.NormaliseValues(normalisedDefinitions.Value!, values);
            if (!result.IsValid)
            {
                return new(null, result.Errors.Select(error =>
                    new ValidationError($"{path}.{error.Property}", error.Message)).ToArray());
            }
            if (normalisedDefinitions.Value!.Count == 0) { return new(snapshotJson, []); }
            var root = JsonNode.Parse(snapshotJson)!;
            root["payload"]!["fieldDefinitions"] = JsonNode.Parse(CardFieldJsonCodec.SerialiseDefinitions(normalisedDefinitions.Value));
            root["payload"]!["fields"] = JsonNode.Parse(CardFieldJsonCodec.SerialiseValues(result.Value!));
            return new(root.ToJsonString(), []);
        }
        catch (JsonException)
        {
            return new(null, [new ValidationError(path, "Archived custom fields are invalid.")]);
        }
    }

    public static string RemapTypeId(string snapshotJson, IReadOnlyDictionary<int, int> typeIds)
    {
        using var document = ReadFieldSnapshot(snapshotJson);
        if (document is null) { return snapshotJson; }
        var payload = document.RootElement.GetProperty("payload");
        if ((!payload.TryGetProperty("fieldDefinitions", out var definitions) || definitions.GetArrayLength() == 0)
            && (!payload.TryGetProperty("fields", out var values) || !values.EnumerateObject().Any()))
        {
            return snapshotJson;
        }
        var sourceId = 0;
        if (payload.TryGetProperty("cardTypeId", out var id) && id.ValueKind == JsonValueKind.Number)
        {
            id.TryGetInt32(out sourceId);
        }
        // Unmapped IDs must not accidentally identify unrelated types in the receiving database.
        var root = JsonNode.Parse(snapshotJson)!;
        root["payload"]!["cardTypeId"] = typeIds.GetValueOrDefault(sourceId);
        return root.ToJsonString();
    }

    private static JsonDocument? ReadFieldSnapshot(string snapshotJson)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(snapshotJson); }
        catch (JsonException) { return null; }
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.String
            && schema.GetString() == "archived-card"
            && root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out var number) && number == 1
            && root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object
            && (payload.TryGetProperty("fieldDefinitions", out _) || payload.TryGetProperty("fields", out _)))
        {
            return document;
        }
        // Existing opaque/unknown snapshots keep their established import behaviour.
        document.Dispose();
        return null;
    }
}
