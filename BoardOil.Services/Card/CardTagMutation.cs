using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Services.Style;

namespace BoardOil.Services.Card;

internal static class CardTagMutation
{
    public static void ReplaceTags(EntityBoardCard card, IReadOnlyList<EntityTag> tags)
    {
        card.CardTags.Clear();
        foreach (var tag in tags.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            card.CardTags.Add(new EntityCardTag { Tag = tag });
        }
    }

    public static async Task<IReadOnlyList<EntityTag>> ResolveTagsAsync(
        int boardId,
        IReadOnlyList<string> tagNames,
        ITagRepository tagRepository,
        IBoardStyleDefaultService styleDefaultService)
    {
        var resolvedTags = new List<EntityTag>();
        var processedNormalisedNames = new HashSet<string>(StringComparer.Ordinal);
        var normalisedTagNames = NormalizeTags(tagNames);
        if (normalisedTagNames.Count == 0)
        {
            return resolvedTags;
        }

        var existingTags = await tagRepository.GetAllForBoardAsync(boardId);
        var existingTagsByNormalisedName = existingTags.ToDictionary(x => x.NormalisedName, StringComparer.Ordinal);
        var styleCandidates = existingTags
            .Select(x => new BoardStyleDefaultCandidate(x.StyleName, x.StylePropertiesJson))
            .ToList();
        foreach (var tagName in normalisedTagNames)
        {
            var normalisedName = NormaliseTagName(tagName);
            if (!processedNormalisedNames.Add(normalisedName))
            {
                continue;
            }

            if (existingTagsByNormalisedName.TryGetValue(normalisedName, out var existingTag))
            {
                resolvedTags.Add(existingTag);
                continue;
            }

            var defaultStyle = styleDefaultService.BuildCreateDefaultStyle(styleCandidates);
            var createdTag = new EntityTag
            {
                BoardId = boardId,
                Name = tagName,
                NormalisedName = normalisedName,
                StyleName = defaultStyle.StyleName,
                StylePropertiesJson = defaultStyle.StylePropertiesJson,
            };
            tagRepository.Add(createdTag);
            resolvedTags.Add(createdTag);
            existingTagsByNormalisedName[normalisedName] = createdTag;
            styleCandidates.Add(new BoardStyleDefaultCandidate(createdTag.StyleName, createdTag.StylePropertiesJson));
        }

        return resolvedTags
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string NormaliseTagName(string tagName) =>
        tagName.ToUpperInvariant();

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string> tagNames)
    {
        return tagNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }
}
