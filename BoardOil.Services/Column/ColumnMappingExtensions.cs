using BoardOil.Contracts.Column;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Column;

public static class ColumnMappingExtensions
{
    public static ColumnDto ToColumnDto(this ColumnRecord column, int position) =>
        new(
            column.Id,
            column.Title,
            position,
            column.CreatedAtUtc,
            column.UpdatedAtUtc);

    public static ColumnDto ToColumnDto(this EntityBoardColumn column, int position) =>
        new(
            column.Id,
            column.Title,
            position,
            column.CreatedAtUtc,
            column.UpdatedAtUtc);
}
