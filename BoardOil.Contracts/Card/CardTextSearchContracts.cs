namespace BoardOil.Contracts.Card;

public sealed record CardTextSearchRequest(string Query, int Offset = 0, int Limit = 20);

public sealed record CardSearchSummaryDto(
    int Id,
    string Title,
    int ColumnId,
    int CardTypeId,
    string? ExternalUrl,
    IReadOnlyList<string> TagNames,
    string? SlickName);

public sealed record CardTextSearchResultDto(
    IReadOnlyList<CardSearchSummaryDto> Cards,
    int TotalCount,
    int Offset,
    int Limit);
