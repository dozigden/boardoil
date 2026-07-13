using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Services.Style;

namespace BoardOil.Services.Card;

internal static class CardSlickMutation
{
    public static async Task<EntitySlick?> ResolveSlickAsync(
        int boardId,
        string? slickName,
        ISlickRepository slickRepository,
        IBoardStyleDefaultService styleDefaultService)
    {
        if (string.IsNullOrWhiteSpace(slickName))
        {
            return null;
        }

        var canonicalName = slickName.Trim();
        var normalisedName = canonicalName.ToUpperInvariant();
        var existingSlicks = await slickRepository.GetAllForBoardAsync(boardId);
        var existing = existingSlicks.FirstOrDefault(x => x.NormalisedName == normalisedName);
        if (existing is not null)
        {
            return existing;
        }

        var defaultStyle = styleDefaultService.BuildCreateDefaultStyle(
            existingSlicks.Select(x => new BoardStyleDefaultCandidate(x.StyleName, x.StylePropertiesJson)));
        var created = new EntitySlick
        {
            BoardId = boardId,
            Name = canonicalName,
            NormalisedName = normalisedName,
            StyleName = defaultStyle.StyleName,
            StylePropertiesJson = defaultStyle.StylePropertiesJson,
        };
        slickRepository.Add(created);
        return created;
    }
}
