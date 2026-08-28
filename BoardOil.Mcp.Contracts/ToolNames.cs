namespace BoardOil.Mcp.Contracts;

public static class ToolNames
{
    // Canonical names are OpenAI-compatible (^[a-zA-Z0-9_-]+$).
    public const string BoardList = "board_list";
    public const string BoardGet = "board_get";
    public const string IdentityGet = "identity_get";
    public const string CardOptionsGet = "card_options_get";
    public const string CardGet = "card_get";
    public const string CardCreate = "card_create";
    public const string CardUpdate = "card_update";
    public const string CardMove = "card_move";
    public const string CardDelete = "card_delete";
    public const string CardCommentCreate = "card_comment_create";
    public const string TagCreate = "tag_create";
    public const string TagUpdate = "tag_update";
    public const string TagDelete = "tag_delete";
}
