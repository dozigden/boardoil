using BoardOil.Contracts.Slick;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Slick;

public static class SlickMappingExtensions
{
    public static SlickDto ToSlickDto(this EntitySlick slick) =>
        new(
            slick.Id,
            slick.Name,
            slick.StyleName,
            slick.StylePropertiesJson,
            slick.CreatedAtUtc,
            slick.UpdatedAtUtc);
}
