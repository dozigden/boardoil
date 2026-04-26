namespace BoardOil.Mcp.Contracts;

public static class ToolNames
{
    // Canonical names are OpenAI-compatible (^[a-zA-Z0-9_-]+$).
    public const string BoardList = "board_list";
    public const string BoardGet = "board_get";
    public const string CardGet = "card_get";
    public const string ColumnsList = "columns_list";
    public const string CardCreate = "card_create";
    public const string CardUpdate = "card_update";
    public const string CardMove = "card_move";
    public const string CardDelete = "card_delete";
}
