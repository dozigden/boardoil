using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Tag;
using BoardOil.Services.Tag;

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

    public static async Task<IReadOnlyList<EntityTag>> ResolveTagsAsync(int boardId, IReadOnlyList<string> tagNames, ITagRepository tagRepository)
    {
        var resolvedTags = new List<EntityTag>();
        var processedNormalisedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tagName in NormalizeTags(tagNames))
        {
            var normalisedName = NormaliseTagName(tagName);
            if (!processedNormalisedNames.Add(normalisedName))
            {
                continue;
            }

            var existingTag = await tagRepository.GetByNormalisedNameAsync(boardId, normalisedName);
            if (existingTag is not null)
            {
                resolvedTags.Add(existingTag);
                continue;
            }

            var createdTag = new EntityTag
            {
                BoardId = boardId,
                Name = tagName,
                NormalisedName = normalisedName,
                StyleName = TagStyleSchemaValidator.PresetsStyleName,
                StylePropertiesJson = TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(TagStyleSchemaValidator.PresetsStyleName),
            };
            tagRepository.Add(createdTag);
            resolvedTags.Add(createdTag);
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
