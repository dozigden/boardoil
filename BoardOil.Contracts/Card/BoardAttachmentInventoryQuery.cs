namespace BoardOil.Contracts.Card;

public sealed record BoardAttachmentInventoryQuery(
    int Offset = 0,
    int Limit = 50,
    string Sort = "date",
    string Direction = "desc",
    string State = "both");
