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

    public const string TagUpdateInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "currentTagName": {
          "type": "string",
          "minLength": 1,
          "maxLength": 40,
          "description": "Current tag name, resolved case-insensitively from card_options_get.tags[].name."
        },
        "name": {
          "type": "string",
          "minLength": 1,
          "maxLength": 40,
          "description": "New tag name. Omit to preserve the current name."
        },
        "emoji": {
          "type": ["string", "null"],
          "description": "New tag emoji. Omit to preserve it or use null to clear it."
        },
        "style": {
          "description": "Complete replacement style. Omit to preserve the current style.",
          "oneOf": [
            {
              "type": "object",
              "properties": {
                "styleName": { "const": "auto" }
              },
              "required": ["styleName"],
              "additionalProperties": false
            },
            {
              "type": "object",
              "properties": {
                "styleName": { "const": "presets" },
                "presetIndex": { "type": "integer", "minimum": 0, "maximum": 11 }
              },
              "required": ["styleName", "presetIndex"],
              "additionalProperties": false
            },
            {
              "type": "object",
              "properties": {
                "styleName": { "const": "solid" },
                "backgroundColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
                "textColorMode": { "type": "string", "enum": ["auto", "custom"] },
                "textColor": { "type": ["string", "null"], "pattern": "^#[0-9A-Fa-f]{6}$" },
                "borderMode": { "type": "string", "enum": ["auto", "custom", "none"] },
                "borderColor": { "type": ["string", "null"], "pattern": "^#[0-9A-Fa-f]{6}$" }
              },
              "required": ["styleName", "backgroundColor", "textColorMode", "borderMode"],
              "allOf": [
                {
                  "if": { "properties": { "textColorMode": { "const": "custom" } } },
                  "then": {
                    "properties": { "textColor": { "type": "string" } },
                    "required": ["textColor"]
                  },
                  "else": { "properties": { "textColor": { "type": "null" } } }
                },
                {
                  "if": { "properties": { "borderMode": { "const": "custom" } } },
                  "then": {
                    "properties": { "borderColor": { "type": "string" } },
                    "required": ["borderColor"]
                  },
                  "else": { "properties": { "borderColor": { "type": "null" } } }
                }
              ],
              "additionalProperties": false
            },
            {
              "type": "object",
              "properties": {
                "styleName": { "const": "gradient" },
                "leftColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
                "rightColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
                "textColorMode": { "type": "string", "enum": ["auto", "custom"] },
                "textColor": { "type": ["string", "null"], "pattern": "^#[0-9A-Fa-f]{6}$" },
                "borderMode": { "type": "string", "enum": ["auto", "custom", "none"] },
                "borderColor": { "type": ["string", "null"], "pattern": "^#[0-9A-Fa-f]{6}$" }
              },
              "required": ["styleName", "leftColor", "rightColor", "textColorMode", "borderMode"],
              "allOf": [
                {
                  "if": { "properties": { "textColorMode": { "const": "custom" } } },
                  "then": {
                    "properties": { "textColor": { "type": "string" } },
                    "required": ["textColor"]
                  },
                  "else": { "properties": { "textColor": { "type": "null" } } }
                },
                {
                  "if": { "properties": { "borderMode": { "const": "custom" } } },
                  "then": {
                    "properties": { "borderColor": { "type": "string" } },
                    "required": ["borderColor"]
                  },
                  "else": { "properties": { "borderColor": { "type": "null" } } }
                }
              ],
              "additionalProperties": false
            }
          ]
        }
      },
      "required": ["boardId", "currentTagName"],
      "anyOf": [
        { "required": ["name"] },
        { "required": ["emoji"] },
        { "required": ["style"] }
      ],
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

    public const string TagUpdateOutput = """
    {
      "type": "object",
      "properties": {
        "tag": {
          "type": "object",
          "properties": {
            "id": { "type": "integer" },
            "name": { "type": "string" },
            "emoji": { "type": ["string", "null"] },
            "style": {
              "type": "object",
              "properties": {
                "styleName": { "type": "string", "enum": ["auto", "presets", "solid", "gradient"] },
                "presetIndex": { "type": "integer", "minimum": 0, "maximum": 11 },
                "backgroundColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
                "leftColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
                "rightColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
                "textColorMode": { "type": "string", "enum": ["auto", "custom"] },
                "textColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
                "borderMode": { "type": "string", "enum": ["auto", "custom", "none"] },
                "borderColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" }
              },
              "required": ["styleName"],
              "additionalProperties": false
            },
            "createdAtUtc": { "type": "string", "format": "date-time" },
            "updatedAtUtc": { "type": "string", "format": "date-time" }
          },
          "required": ["id", "name", "emoji", "style", "createdAtUtc", "updatedAtUtc"],
          "additionalProperties": false
        },
        "outcome": { "type": "string", "enum": ["updated"] }
      },
      "required": ["tag", "outcome"],
      "additionalProperties": false
    }
    """;
}
