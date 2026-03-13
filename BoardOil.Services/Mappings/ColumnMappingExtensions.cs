using BoardOil.Ef.Entities;
using BoardOil.Services.Contracts;

namespace BoardOil.Services.Mappings;

public static class ColumnMappingExtensions
{
    public static ColumnDto ToColumnDto(this BoardColumn column) =>
        new(
            column.Id,
            column.Title,
            column.Position,
            column.CreatedAtUtc,
            column.UpdatedAtUtc);
}
