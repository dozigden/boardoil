using BoardOil.Contracts.Board;
using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Slick;
using BoardOil.Contracts.Tag;

namespace BoardOil.Contracts.Card;

public sealed record BoardCardOptionsDto(
    int Id,
    IReadOnlyList<ColumnDto> Columns,
    IReadOnlyList<BoardMemberDto> Members,
    IReadOnlyList<CardTypeDto> CardTypes,
    int DefaultCardTypeId,
    IReadOnlyList<TagDto> Tags,
    IReadOnlyList<SlickDto> Slicks);
