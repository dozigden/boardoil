namespace BoardOil.Mcp.Contracts.Schemas;

public static class ToolSchemas
{
    public const string BoardGetInput = """
    {
      "type": "object",
      "properties": {
        "id": { "type": "integer", "minimum": 1 }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """;

    public const string ColumnsListInput = BoardGetInput;

    public const string CardCreateInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "columnId": { "type": "integer", "minimum": 1 },
        "title": { "type": "string", "minLength": 1, "maxLength": 200 },
        "description": { "type": "string", "maxLength": 20000 },
        "tagNames": {
          "type": ["array", "null"],
          "items": { "type": "string", "minLength": 1, "maxLength": 80 },
          "maxItems": 20
        }
      },
      "required": ["boardId", "columnId", "title", "description"],
      "additionalProperties": false
    }
    """;

    public const string CardUpdateInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": { "type": "integer", "minimum": 1 },
        "title": { "type": "string", "minLength": 1, "maxLength": 200 },
        "description": { "type": "string", "maxLength": 20000 },
        "tagNames": {
          "type": "array",
          "items": { "type": "string", "minLength": 1, "maxLength": 80 },
          "maxItems": 20
        }
      },
      "required": ["boardId", "id", "title", "description", "tagNames"],
      "additionalProperties": false
    }
    """;

    public const string CardMoveInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": { "type": "integer", "minimum": 1 },
        "columnId": { "type": "integer", "minimum": 1 },
        "afterId": { "type": ["integer", "null"], "minimum": 1 }
      },
      "required": ["boardId", "id", "columnId"],
      "additionalProperties": false
    }
    """;

    public const string CardMoveByColumnNameInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": { "type": "integer", "minimum": 1 },
        "columnTitle": { "type": "string", "minLength": 1, "maxLength": 200 },
        "afterId": { "type": ["integer", "null"], "minimum": 1 }
      },
      "required": ["boardId", "id", "columnTitle"],
      "additionalProperties": false
    }
    """;

    public const string CardDeleteInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": { "type": "integer", "minimum": 1 }
      },
      "required": ["boardId", "id"],
      "additionalProperties": false
    }
    """;

    public const string SuccessOutput = """
    {
      "type": "object",
      "properties": {
        "success": { "const": true },
        "data": { "type": "object" },
        "error": { "type": "null" }
      },
      "required": ["success", "data", "error"]
    }
    """;

    public const string ErrorOutput = """
    {
      "type": "object",
      "properties": {
        "success": { "const": false },
        "data": { "type": "null" },
        "error": {
          "type": "object",
          "properties": {
            "code": { "type": "string" },
            "message": { "type": "string" },
            "statusCode": { "type": "integer" }
          },
          "required": ["code", "message", "statusCode"]
        }
      },
      "required": ["success", "data", "error"]
    }
    """;
}
