using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Card;

public static class CardTypeDefaults
{
    public const string SystemTypeName = "Story";
    public const string DefaultStyleName = "solid";
    public const string DefaultStylePropertiesJson = """{"backgroundColor":"#FFFFFF","textColorMode":"auto"}""";

    public static EntityCardType CreateSystemForBoard(EntityBoard board, DateTime nowUtc) =>
        new()
        {
            Board = board,
            Name = SystemTypeName,
            Emoji = null,
            StyleName = DefaultStyleName,
            StylePropertiesJson = DefaultStylePropertiesJson,
            IsSystem = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
}
