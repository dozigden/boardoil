using BoardOil.Contracts.Column;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Column;

public static class ColumnMappingExtensions
{
    public static ColumnDto ToColumnDto(this EntityBoardColumn column) =>
        new(
            column.Id,
            column.Title,
            column.SortKey,
            column.CreatedAtUtc,
            column.UpdatedAtUtc);
}
