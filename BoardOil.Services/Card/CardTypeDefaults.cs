using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Card;

public static class CardTypeDefaults
{
    public const string SystemTypeName = "Story";

    public static EntityCardType CreateSystemForBoard(EntityBoard board, DateTime nowUtc) =>
        new()
        {
            Board = board,
            Name = SystemTypeName,
            Emoji = null,
            IsSystem = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
}
