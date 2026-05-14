using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Card;

    public static class CardTypeDefaults
{
    public const string SystemTypeName = "Story";
    public const string DefaultStyleName = "auto";
    public const string DefaultStylePropertiesJson = "{}";

    public static EntityCardType CreateSystemForBoard(EntityBoard board, DateTime nowUtc) =>
        new()
        {
            Board = board,
            Name = SystemTypeName,
            Emoji = null,
            StyleName = DefaultStyleName,
            StylePropertiesJson = DefaultStylePropertiesJson,
            IsSystem = true,
        };
}
