using BoardOil.Ef.Entities;
using BoardOil.Services.Contracts;

namespace BoardOil.Services.Mappings;

public static class ColumnMappingExtensions
{
    public static ColumnDto ToColumnDto(this BoardColumn column, int position) =>
        new(
            column.Id,
            column.Title,
            position,
            column.CreatedAtUtc,
            column.UpdatedAtUtc);
}
