namespace BoardOil.Mcp.Contracts.Schemas;

public static class ToolSchemas
{
    public const string CardSearchInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "query": { "type": "string", "minLength": 1, "pattern": "\\S", "description": "Literal substring; whitespace is preserved. Matches card number, title, description, or external URL, ignoring case." },
        "offset": { "type": "integer", "minimum": 0, "default": 0 },
        "limit": { "type": "integer", "minimum": 1, "maximum": 100, "default": 20 }
      },
      "required": ["boardId", "query"],
      "additionalProperties": false
    }
    """;

    public const string CardSearchOutput = """
    {
      "type": "object",
      "properties": {
        "cards": {
          "type": "array",
          "maxItems": 100,
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "integer", "minimum": 1, "description": "Board-scoped card number." },
              "title": { "type": "string" },
              "columnId": { "type": "integer", "minimum": 1 },
              "cardTypeId": { "type": "integer", "minimum": 1 },
              "externalUrl": { "type": ["string", "null"] },
              "tagNames": { "type": "array", "items": { "type": "string" } },
              "slickName": { "type": ["string", "null"] }
            },
            "required": ["id", "title", "columnId", "cardTypeId", "externalUrl", "tagNames", "slickName"],
            "additionalProperties": false
          }
        },
        "totalCount": { "type": "integer", "minimum": 0 },
        "offset": { "type": "integer", "minimum": 0 },
        "limit": { "type": "integer", "minimum": 1, "maximum": 100 }
      },
      "required": ["cards", "totalCount", "offset", "limit"],
      "additionalProperties": false
    }
    """;

    private const string AutoStyleSchema = """
    {
      "type": "object",
      "properties": {
        "styleName": { "const": "auto" }
      },
      "required": ["styleName"],
      "additionalProperties": false
    }
    """;

    private const string PresetStyleSchema = """
    {
      "type": "object",
      "properties": {
        "styleName": { "const": "presets" },
        "presetIndex": { "type": "integer", "minimum": 0, "maximum": 11 }
      },
      "required": ["styleName", "presetIndex"],
      "additionalProperties": false
    }
    """;

    private const string SolidStyleInputSchema = """
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
    }
    """;

    private const string GradientStyleInputSchema = """
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
    """;

    private const string SolidStyleOutputSchema = """
    {
      "type": "object",
      "properties": {
        "styleName": { "const": "solid" },
        "backgroundColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "textColorMode": { "type": "string", "enum": ["auto", "custom"] },
        "textColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "borderMode": { "type": "string", "enum": ["auto", "custom", "none"] },
        "borderColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" }
      },
      "required": ["styleName", "backgroundColor", "textColorMode", "borderMode"],
      "allOf": [
        {
          "if": { "properties": { "textColorMode": { "const": "custom" } } },
          "then": { "required": ["textColor"] },
          "else": { "not": { "required": ["textColor"] } }
        },
        {
          "if": { "properties": { "borderMode": { "const": "custom" } } },
          "then": { "required": ["borderColor"] },
          "else": { "not": { "required": ["borderColor"] } }
        }
      ],
      "additionalProperties": false
    }
    """;

    private const string GradientStyleOutputSchema = """
    {
      "type": "object",
      "properties": {
        "styleName": { "const": "gradient" },
        "leftColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "rightColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "textColorMode": { "type": "string", "enum": ["auto", "custom"] },
        "textColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "borderMode": { "type": "string", "enum": ["auto", "custom", "none"] },
        "borderColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" }
      },
      "required": ["styleName", "leftColor", "rightColor", "textColorMode", "borderMode"],
      "allOf": [
        {
          "if": { "properties": { "textColorMode": { "const": "custom" } } },
          "then": { "required": ["textColor"] },
          "else": { "not": { "required": ["textColor"] } }
        },
        {
          "if": { "properties": { "borderMode": { "const": "custom" } } },
          "then": { "required": ["borderColor"] },
          "else": { "not": { "required": ["borderColor"] } }
        }
      ],
      "additionalProperties": false
    }
    """;

    private static readonly string TagStyleInput = $$"""
    {
      "oneOf": [
        {{AutoStyleSchema}},
        {{PresetStyleSchema}},
        {{SolidStyleInputSchema}},
        {{GradientStyleInputSchema}}
      ]
    }
    """;

    private static readonly string TagStyleOutput = $$"""
    {
      "oneOf": [
        {{AutoStyleSchema}},
        {{PresetStyleSchema}},
        {{SolidStyleOutputSchema}},
        {{GradientStyleOutputSchema}}
      ]
    }
    """;

    private static readonly string SlickStyleInput = $$"""
    {
      "oneOf": [
        {{PresetStyleSchema}},
        {{SolidStyleInputSchema}}
      ]
    }
    """;

    private static readonly string SlickStyleOutput = $$"""
    {
      "oneOf": [
        {{PresetStyleSchema}},
        {{SolidStyleOutputSchema}}
      ]
    }
    """;

    private static readonly string TagSnapshotOutput = $$"""
    {
      "type": "object",
      "properties": {
        "id": { "type": "integer" },
        "name": { "type": "string" },
        "emoji": { "type": ["string", "null"] },
        "style": {{TagStyleOutput}},
        "createdAtUtc": { "type": "string", "format": "date-time" },
        "updatedAtUtc": { "type": "string", "format": "date-time" }
      },
      "required": ["id", "name", "emoji", "style", "createdAtUtc", "updatedAtUtc"],
      "additionalProperties": false
    }
    """;

    private static readonly string SlickSnapshotOutput = $$"""
    {
      "type": "object",
      "properties": {
        "id": { "type": "integer" },
        "name": { "type": "string" },
        "style": {{SlickStyleOutput}},
        "createdAtUtc": { "type": "string", "format": "date-time" },
        "updatedAtUtc": { "type": "string", "format": "date-time" }
      },
      "required": ["id", "name", "style", "createdAtUtc", "updatedAtUtc"],
      "additionalProperties": false
    }
    """;

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

    public const string CardAttachmentDeleteInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "cardId": { "type": "integer", "minimum": 1, "description": "Board-scoped number of the live card owning this attachment." },
        "id": { "type": "integer", "minimum": 1, "description": "Attachment ID from card_get.attachments[].id." }
      },
      "required": ["boardId", "cardId", "id"],
      "additionalProperties": false
    }
    """;

    public const string CardAttachmentDeleteOutput = """
    {
      "type": "object",
      "properties": {
        "id": { "type": "integer", "minimum": 1 },
        "outcome": { "type": "string", "enum": ["deleted"] }
      },
      "required": ["id", "outcome"],
      "additionalProperties": false
    }
    """;

    public const string CardAttachmentDownloadInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": { "type": "integer", "minimum": 1, "description": "Attachment ID from card_get.attachments[].id." }
      },
      "required": ["boardId", "id"],
      "additionalProperties": false
    }
    """;

    public const string CardAttachmentDownloadOutput = """
    {
      "type": "object",
      "properties": {
        "url": { "type": "string", "format": "uri" },
        "method": { "type": "string", "enum": ["GET"] },
        "headers": {
          "type": "object",
          "properties": { "Authorization": { "type": "string", "pattern": "^BoardOilAttachment ", "description": "Secret credential using the dedicated BoardOilAttachment scheme; send only as a header to the returned URL. Do not log or share it." } },
          "required": ["Authorization"],
          "additionalProperties": false
        },
        "expiresAtUtc": { "type": "string", "format": "date-time" }
      },
      "required": ["url", "method", "headers", "expiresAtUtc"],
      "additionalProperties": false
    }
    """;

    public const string CardAttachmentUploadInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "cardId": { "type": "integer", "minimum": 1, "description": "Board-scoped number of the live card that will own the attachment." },
        "fileName": { "type": "string", "minLength": 1, "maxLength": 255, "description": "Original filename. Paths are reduced to their final filename component." },
        "contentType": { "type": ["string", "null"], "maxLength": 255, "description": "Media type to bind to the upload; invalid or omitted values become application/octet-stream." },
        "byteLength": { "type": "integer", "minimum": 0, "description": "Exact number of raw body bytes that will be uploaded. The configured attachment limit also applies." }
      },
      "required": ["boardId", "cardId", "fileName", "byteLength"],
      "additionalProperties": false
    }
    """;

    public const string CardAttachmentUploadOutput = """
    {
      "type": "object",
      "properties": {
        "url": { "type": "string", "format": "uri" },
        "method": { "type": "string", "enum": ["PUT"] },
        "headers": {
          "type": "object",
          "properties": {
            "Authorization": { "type": "string", "pattern": "^BoardOilAttachment ", "description": "Secret credential using the dedicated BoardOilAttachment scheme; send only as a header to the returned URL. Do not log or share it." },
            "Content-Type": { "type": "string", "description": "Send this exact media type with the raw request body." }
          },
          "required": ["Authorization", "Content-Type"],
          "additionalProperties": false
        },
        "byteLength": { "type": "integer", "minimum": 0 },
        "markdownSnippet": { "type": "string", "description": "Ready-to-insert card-description Markdown using the canonical reserved filename. Insert it only after the HTTP PUT succeeds; replace the default alt text with a useful description when appropriate." },
        "expiresAtUtc": { "type": "string", "format": "date-time" }
      },
      "required": ["url", "method", "headers", "byteLength", "markdownSnippet", "expiresAtUtc"],
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
        "description": { "type": "string", "maxLength": 20000, "description": "Card description Markdown. BoardOil-hosted images require a saved card: create the card first, then use card_attachment_upload and card_update with the returned markdownSnippet." },
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
        "description": { "type": "string", "maxLength": 20000, "description": "Card description Markdown. To add a BoardOil-hosted image, use card_attachment_upload, complete its HTTP PUT, then insert the returned markdownSnippet here." },
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

    public static readonly string TagCreateInput = $$"""
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "name": { "type": "string", "minLength": 1, "maxLength": 40 },
        "emoji": {
          "type": ["string", "null"],
          "description": "Tag emoji, or null for no emoji."
        },
        "style": {{TagStyleInput}}
      },
      "required": ["boardId", "name", "emoji", "style"],
      "additionalProperties": false
    }
    """;

    public static readonly string TagUpdateInput = $$"""
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
        "style": {{TagStyleInput}}
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

    public const string TagDeleteInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": {
          "type": "integer",
          "minimum": 1,
          "description": "Resolve from card_options_get.tags[].id."
        }
      },
      "required": ["boardId", "id"],
      "additionalProperties": false
    }
    """;

    public static readonly string SlickCreateInput = $$"""
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "name": { "type": "string", "minLength": 1, "maxLength": 40 },
        "style": {{SlickStyleInput}}
      },
      "required": ["boardId", "name", "style"],
      "additionalProperties": false
    }
    """;

    public static readonly string SlickUpdateInput = $$"""
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": {
          "type": "integer",
          "minimum": 1,
          "description": "Resolve from card_options_get.slicks[].id."
        },
        "name": {
          "type": "string",
          "minLength": 1,
          "maxLength": 40,
          "description": "New slick name. Omit to preserve the current name."
        },
        "style": {{SlickStyleInput}}
      },
      "required": ["boardId", "id"],
      "anyOf": [
        { "required": ["name"] },
        { "required": ["style"] }
      ],
      "additionalProperties": false
    }
    """;

    public const string SlickDeleteInput = """
    {
      "type": "object",
      "properties": {
        "boardId": { "type": "integer", "minimum": 1 },
        "id": {
          "type": "integer",
          "minimum": 1,
          "description": "Resolve from card_options_get.slicks[].id."
        }
      },
      "required": ["boardId", "id"],
      "additionalProperties": false
    }
    """;

    public static readonly string CardCreateOutput = CreateMutationReceiptSchema("created", "Board-scoped number of the created card.");
    public static readonly string CardUpdateOutput = CreateMutationReceiptSchema("updated", "Board-scoped number of the updated card.");
    public static readonly string CardMoveOutput = CreateMutationReceiptSchema("moved", "Board-scoped number of the moved card.");
    public static readonly string CardDeleteOutput = CreateMutationReceiptSchema("deleted", "Board-scoped number of the deleted card.");
    public static readonly string CardCommentCreateOutput = CreateMutationReceiptSchema("created", "Generated comment ID, not the card number.");

    private static string CreateMutationReceiptSchema(string outcome, string idDescription) => $$"""
    {
      "type": "object",
      "properties": {
        "id": { "type": "integer", "minimum": 1, "description": "{{idDescription}}" },
        "outcome": { "type": "string", "enum": ["{{outcome}}"] }
      },
      "required": ["id", "outcome"],
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

    public static readonly string CardOptionsGetOutput = $$"""
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
              "id": { "type": "integer" },
              "name": { "type": "string" },
              "emoji": { "type": ["string", "null"] },
              "style": {{TagStyleOutput}}
            },
            "required": ["id", "name", "emoji", "style"],
            "additionalProperties": false
          }
        },
        "slicks": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "integer" },
              "name": { "type": "string" },
              "style": {{SlickStyleOutput}}
            },
            "required": ["id", "name", "style"],
            "additionalProperties": false
          }
        }
      },
      "required": ["id", "columns", "members", "cardTypes", "defaultCardTypeId", "tags", "slicks"],
      "additionalProperties": false
    }
    """;

    public static readonly string TagCreateOutput = $$"""
    {
      "type": "object",
      "properties": {
        "tag": {{TagSnapshotOutput}},
        "outcome": { "type": "string", "enum": ["created", "existing"] }
      },
      "required": ["tag", "outcome"],
      "additionalProperties": false
    }
    """;

    public static readonly string TagUpdateOutput = $$"""
    {
      "type": "object",
      "properties": {
        "tag": {{TagSnapshotOutput}},
        "outcome": { "type": "string", "enum": ["updated"] }
      },
      "required": ["tag", "outcome"],
      "additionalProperties": false
    }
    """;

    public const string TagDeleteOutput = """
    {
      "type": "object",
      "properties": {
        "outcome": { "type": "string", "enum": ["deleted"] }
      },
      "required": ["outcome"],
      "additionalProperties": false
    }
    """;

    public static readonly string SlickCreateOutput = $$"""
    {
      "type": "object",
      "properties": {
        "slick": {{SlickSnapshotOutput}},
        "outcome": { "type": "string", "enum": ["created", "existing"] }
      },
      "required": ["slick", "outcome"],
      "additionalProperties": false
    }
    """;

    public static readonly string SlickUpdateOutput = $$"""
    {
      "type": "object",
      "properties": {
        "slick": {{SlickSnapshotOutput}},
        "outcome": { "type": "string", "enum": ["updated"] }
      },
      "required": ["slick", "outcome"],
      "additionalProperties": false
    }
    """;

    public const string SlickDeleteOutput = """
    {
      "type": "object",
      "properties": {
        "outcome": { "type": "string", "enum": ["deleted"] }
      },
      "required": ["outcome"],
      "additionalProperties": false
    }
    """;
}
