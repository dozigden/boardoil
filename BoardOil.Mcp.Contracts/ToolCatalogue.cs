using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Mcp.Contracts;

public static class ToolCatalogue
{
    public static readonly IReadOnlyList<McpToolDefinition> Definitions =
    [
        new(ToolNames.BoardGet, "Get a board snapshot including columns and cards.", ToolSchemas.BoardGetInput, ToolSchemas.SuccessOutput),
        new(ToolNames.ColumnsList, "List columns for a board so an agent can resolve dynamic states.", ToolSchemas.ColumnsListInput, ToolSchemas.SuccessOutput),
        new(ToolNames.CardCreate, "Create a card in a specific column.", ToolSchemas.CardCreateInput, ToolSchemas.SuccessOutput),
        new(ToolNames.CardUpdate, "Update card title, description, and tags.", ToolSchemas.CardUpdateInput, ToolSchemas.SuccessOutput),
        new(ToolNames.CardMove, "Move card by target column id and optional sibling anchor.", ToolSchemas.CardMoveInput, ToolSchemas.SuccessOutput),
        new(ToolNames.CardMoveByColumnName, "Move card by dynamic column title.", ToolSchemas.CardMoveByColumnNameInput, ToolSchemas.SuccessOutput),
        new(ToolNames.CardDelete, "Delete a card.", ToolSchemas.CardDeleteInput, ToolSchemas.SuccessOutput)
    ];
}
