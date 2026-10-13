namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityCustomFieldCardType
{
    public int Id { get; set; }
    // Shared by both foreign keys so assignments cannot cross boards.
    public int BoardId { get; set; }
    public int CustomFieldId { get; set; }
    public int CardTypeId { get; set; }
    public EntityCustomField CustomField { get; set; } = null!;
    public EntityCardType CardType { get; set; } = null!;
}
