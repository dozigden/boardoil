using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Contracts.Tag;

namespace BoardOil.Services.Tag;

public static class TagMappingExtensions
{
    public static TagDto ToTagDto(this EntityTag tag) =>
        new(
            tag.Name,
            tag.StyleName,
            tag.StylePropertiesJson,
            tag.CreatedAtUtc,
            tag.UpdatedAtUtc);
}
