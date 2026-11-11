namespace BoardOil.Mcp.Contracts.Schemas;

public static partial class ToolSchemas
{
    public const string BoardReorderInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "columns": {
          "type": "array",
          "description": "Every current column exactly once. Array order does not change column order.",
          "items": {
            "type": "object",
            "properties": {
              "columnId": { "type": "integer", "minimum": 1 },
              "cardIds": {
                "type": "array",
                "uniqueItems": true,
                "items": { "type": "integer", "minimum": 1 },
                "description": "Complete top-to-bottom list of live board-scoped card numbers for this column; use [] for an empty column. Every live card must appear exactly once across all columns."
              }
            },
            "required": ["columnId", "cardIds"],
            "additionalProperties": false
          }
        }
      },
      "required": ["boardId", "columns"],
      "additionalProperties": false
    }
    """;

    public static readonly string BoardReorderOutput = CreateMutationReceiptSchema("reordered", "ID of the reordered board.");
}
