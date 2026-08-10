namespace BoardOil.Mcp.Contracts.Schemas;

public static class ToolSchemas
{
    public const string BoardListInput = """
    {
      "type": "object",
      "properties": {},
      "additionalProperties": false
    }
    """;

    public const string IdentityGetInput = BoardListInput;

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

    public const string CardOptionsGetInput = """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "integer",
          "minimum": 1,
          "description": "Board ID whose card field options should be returned."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """;

    public const string CardGetInput = """
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

    public const string CardCreateInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "columnId": { "type": "integer", "minimum": 1, "description": "Resolve from card_options_get.columns[].id." },
        "cardTypeId": { "type": ["integer", "null"], "minimum": 1, "description": "Resolve from card_options_get.cardTypes[].id. Omit or use null to select defaultCardTypeId." },
        "assignedUserId": { "type": ["integer", "null"], "minimum": 1, "description": "Resolve from card_options_get.members[].userId, or use null for an unassigned card." },
        "slickName": { "type": ["string", "null"], "maxLength": 40, "description": "Use card_options_get.slicks[].name to reuse an established slick, provide a new name to create one, or use null for no slick." },
        "externalUrl": { "type": ["string", "null"], "format": "uri" },
        "title": { "type": "string", "minLength": 1, "maxLength": 200 },
        "description": { "type": "string", "maxLength": 20000 },
        "tagNames": {
          "type": ["array", "null"],
          "items": { "type": "string", "minLength": 1, "maxLength": 80 },
          "description": "Use card_options_get.tags[].name to reuse established tags. New names create tags."
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
        "columnId": { "type": ["integer", "null"], "minimum": 1, "description": "Resolve from card_options_get.columns[].id, or omit to preserve the current column." },
        "cardTypeId": { "type": "integer", "minimum": 1, "description": "Resolve from card_options_get.cardTypes[].id." },
        "assignedUserId": { "type": ["integer", "null"], "minimum": 1, "description": "Resolve from card_options_get.members[].userId, or use null for an unassigned card." },
        "slickName": { "type": ["string", "null"], "maxLength": 40, "description": "Use card_options_get.slicks[].name to reuse an established slick, provide a new name to create one, or use null for no slick." },
        "externalUrl": { "type": ["string", "null"], "format": "uri" },
        "title": { "type": "string", "minLength": 1, "maxLength": 200 },
        "description": { "type": "string", "maxLength": 20000 },
        "tagNames": {
          "type": "array",
          "items": { "type": "string", "minLength": 1, "maxLength": 80 },
          "description": "Use card_options_get.tags[].name to reuse established tags. New names create tags."
        }
      },
      "required": ["boardId", "id", "cardTypeId", "slickName", "externalUrl", "title", "description", "tagNames"],
      "additionalProperties": false
    }
    """;

    public const string CardMoveInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": { "type": "integer", "minimum": 1 },
        "columnId": { "type": "integer", "minimum": 1, "description": "Resolve from card_options_get.columns[].id." },
        "afterId": { "type": ["integer", "null"], "minimum": 1 }
      },
      "required": ["boardId", "id", "columnId"],
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

    public const string CardCommentCreateInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": { "type": "integer", "minimum": 1 },
        "text": { "type": "string", "minLength": 1, "maxLength": 4000 }
      },
      "required": ["boardId", "id", "text"],
      "additionalProperties": false
    }
    """;

    public const string ObjectOutput = """
    {
      "type": "object"
    }
    """;

    public const string IdentityGetOutput = """
    {
      "type": "object",
      "properties": {
        "user": {
          "type": "object",
          "properties": {
            "id": { "type": "integer" },
            "userName": { "type": "string" },
            "displayName": { "type": "string" },
            "role": { "type": "string" }
          },
          "required": ["id", "userName", "displayName", "role"],
          "additionalProperties": false
        },
        "authentication": {
          "type": "object",
          "properties": {
            "type": { "type": "string", "enum": ["PAT", "OAuth", "None"] },
            "scopes": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["type", "scopes"],
          "additionalProperties": false
        }
      },
      "required": ["user", "authentication"],
      "additionalProperties": false
    }
    """;

    public const string CardOptionsGetOutput = """
    {
      "type": "object",
      "properties": {
        "id": { "type": "integer" },
        "columns": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "integer" },
              "title": { "type": "string" }
            },
            "required": ["id", "title"],
            "additionalProperties": false
          }
        },
        "members": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "userId": { "type": "integer" },
              "userName": { "type": "string" },
              "displayName": { "type": "string" },
              "role": { "type": "string" }
            },
            "required": ["userId", "userName", "displayName", "role"],
            "additionalProperties": false
          }
        },
        "cardTypes": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "integer" },
              "name": { "type": "string" },
              "emoji": { "type": ["string", "null"] }
            },
            "required": ["id", "name", "emoji"],
            "additionalProperties": false
          }
        },
        "defaultCardTypeId": { "type": "integer" },
        "tags": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "name": { "type": "string" },
              "emoji": { "type": ["string", "null"] }
            },
            "required": ["name", "emoji"],
            "additionalProperties": false
          }
        },
        "slicks": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "name": { "type": "string" }
            },
            "required": ["name"],
            "additionalProperties": false
          }
        }
      },
      "required": ["id", "columns", "members", "cardTypes", "defaultCardTypeId", "tags", "slicks"],
      "additionalProperties": false
    }
    """;
}
