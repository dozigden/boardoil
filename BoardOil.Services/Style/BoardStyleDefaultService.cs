using BoardOil.Contracts.Style;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Services.Tag;
using System.Security.Cryptography;
using System.Text.Json;

namespace BoardOil.Services.Style;

public sealed class BoardStyleDefaultService(
    ITagRepository tagRepository,
    ISlickRepository slickRepository) : IBoardStyleDefaultService
{
    public async Task<StyleDefaultDto> GetTagCreateDefaultStyleAsync(int boardId)
    {
        var tags = await tagRepository.GetAllForBoardAsync(boardId);
        return BuildCreateDefaultStyle(tags.Select(x => new BoardStyleDefaultCandidate(x.StyleName, x.StylePropertiesJson)));
    }

    public async Task<StyleDefaultDto> GetSlickCreateDefaultStyleAsync(int boardId)
    {
        var slicks = await slickRepository.GetAllForBoardAsync(boardId);
        return BuildCreateDefaultStyle(slicks.Select(x => new BoardStyleDefaultCandidate(x.StyleName, x.StylePropertiesJson)));
    }

    public StyleDefaultDto BuildCreateDefaultStyle(IEnumerable<BoardStyleDefaultCandidate> existingStyles)
    {
        var presetIndex = PickPresetIndex(existingStyles);
        return new StyleDefaultDto(
            TagStyleSchemaValidator.PresetsStyleName,
            JsonSerializer.Serialize(new
            {
                presetIndex,
                textColorMode = "auto"
            }));
    }

    private static int PickPresetIndex(IEnumerable<BoardStyleDefaultCandidate> existingStyles)
    {
        var usedPresetIndexes = new HashSet<int>();
        foreach (var style in existingStyles)
        {
            var normalisedStyleName = TagStyleSchemaValidator.NormaliseStyleName(style.StyleName);
            if (normalisedStyleName != TagStyleSchemaValidator.PresetsStyleName)
            {
                continue;
            }

            var presetIndex = TryReadPresetIndex(style.StylePropertiesJson);
            if (presetIndex is not null)
            {
                usedPresetIndexes.Add(presetIndex.Value);
            }
        }

        var unusedPresetIndexes = Enumerable
            .Range(0, TagStyleSchemaValidator.PresetCount)
            .Where(x => !usedPresetIndexes.Contains(x))
            .ToList();
        if (unusedPresetIndexes.Count > 0)
        {
            return unusedPresetIndexes[RandomNumberGenerator.GetInt32(unusedPresetIndexes.Count)];
        }

        return RandomNumberGenerator.GetInt32(TagStyleSchemaValidator.PresetCount);
    }

    private static int? TryReadPresetIndex(string stylePropertiesJson)
    {
        try
        {
            using var document = JsonDocument.Parse(stylePropertiesJson);
            if (!document.RootElement.TryGetProperty("presetIndex", out var presetIndexElement))
            {
                return null;
            }

            var presetIndex = ReadPresetIndexValue(presetIndexElement);
            if (presetIndex < 0 || presetIndex >= TagStyleSchemaValidator.PresetCount)
            {
                return null;
            }

            return presetIndex;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static int ReadPresetIndexValue(JsonElement presetIndexElement)
    {
        if (presetIndexElement.ValueKind == JsonValueKind.Number && presetIndexElement.TryGetInt32(out var numericIndex))
        {
            return numericIndex;
        }

        if (presetIndexElement.ValueKind == JsonValueKind.String
            && int.TryParse(presetIndexElement.GetString(), out var stringIndex))
        {
            return stringIndex;
        }

        return -1;
    }
}
