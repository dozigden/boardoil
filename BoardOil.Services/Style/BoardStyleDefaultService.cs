using BoardOil.Contracts.Style;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using System.Security.Cryptography;

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
        var definition = new PresetStyleDefinition(presetIndex);
        return new StyleDefaultDto(
            StyleDefinitionCodec.PresetsStyleName,
            StyleDefinitionCodec.Serialise(definition));
    }

    private static int PickPresetIndex(IEnumerable<BoardStyleDefaultCandidate> existingStyles)
    {
        var usedPresetIndexes = new HashSet<int>();
        foreach (var style in existingStyles)
        {
            var parsed = StyleDefinitionCodec.ParseCompatible(style.StyleName, style.StylePropertiesJson);
            if (parsed.Definition is not PresetStyleDefinition preset)
            {
                continue;
            }

            usedPresetIndexes.Add(preset.PresetIndex);
        }

        var unusedPresetIndexes = Enumerable
            .Range(0, StyleDefinitionCodec.PresetCount)
            .Where(x => !usedPresetIndexes.Contains(x))
            .ToList();
        if (unusedPresetIndexes.Count > 0)
        {
            return unusedPresetIndexes[RandomNumberGenerator.GetInt32(unusedPresetIndexes.Count)];
        }

        return RandomNumberGenerator.GetInt32(StyleDefinitionCodec.PresetCount);
    }
}
