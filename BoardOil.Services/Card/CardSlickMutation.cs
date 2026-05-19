using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Services.Tag;

namespace BoardOil.Services.Card;

internal static class CardSlickMutation
{
    public static async Task<EntitySlick?> ResolveSlickAsync(
        int boardId,
        string? slickName,
        ISlickRepository slickRepository)
    {
        if (string.IsNullOrWhiteSpace(slickName))
        {
            return null;
        }

        var canonicalName = slickName.Trim();
        var normalisedName = canonicalName.ToUpperInvariant();
        var existing = await slickRepository.GetByNormalisedNameAsync(boardId, normalisedName);
        if (existing is not null)
        {
            return existing;
        }

        var created = new EntitySlick
        {
            BoardId = boardId,
            Name = canonicalName,
            NormalisedName = normalisedName,
            StyleName = TagStyleSchemaValidator.PresetsStyleName,
            StylePropertiesJson = TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(TagStyleSchemaValidator.PresetsStyleName),
        };
        slickRepository.Add(created);
        return created;
    }
}
