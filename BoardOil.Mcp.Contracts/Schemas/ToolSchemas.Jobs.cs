namespace BoardOil.Mcp.Contracts.Schemas;

public static partial class ToolSchemas
{
    public const string JobListInput = """
    {
      "type": "object",
      "properties": {
        "offset": {"type": "integer", "minimum": 0, "default": 0},
        "limit": {"type": "integer", "minimum": 1, "maximum": 200, "default": 100}
      },
      "additionalProperties": false
    }
    """;

    public const string JobGetInput = """
    {
      "type": "object",
      "properties": {
        "id": {"type": "integer", "minimum": 1}
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """;

    public const string JobListOutput = """
    {
      "type": "object",
      "properties": {
        "items": {
          "type": "array",
          "maxItems": 200,
          "items": {
            "type": "object",
            "properties": {
              "id": {"type": "integer", "minimum": 1},
              "type": {"type": "string"},
              "status": {"type": "string", "enum": ["pending", "running", "completed", "failed", "cancelled"]},
              "runAfterUtc": {"type": "string", "format": "date-time"},
              "payloadJson": {"type": "string"},
              "resultJson": {"type": "string"},
              "errorMessage": {"type": ["string", "null"]},
              "startedAtUtc": {"type": ["string", "null"], "format": "date-time"},
              "completedAtUtc": {"type": ["string", "null"], "format": "date-time"},
              "userId": {"type": ["integer", "null"]},
              "correlationId": {"type": ["string", "null"]},
              "createdAtUtc": {"type": "string", "format": "date-time"},
              "updatedAtUtc": {"type": "string", "format": "date-time"}
            },
            "required": ["id", "type", "status", "runAfterUtc", "payloadJson", "resultJson", "errorMessage", "startedAtUtc", "completedAtUtc", "userId", "correlationId", "createdAtUtc", "updatedAtUtc"],
            "additionalProperties": false
          }
        },
        "offset": {"type": "integer", "minimum": 0},
        "limit": {"type": "integer", "minimum": 1, "maximum": 200},
        "totalCount": {"type": "integer", "minimum": 0}
      },
      "required": ["items", "offset", "limit", "totalCount"],
      "additionalProperties": false
    }
    """;

    public const string JobGetOutput = """
    {
      "type": "object",
      "properties": {
        "id": {"type": "integer", "minimum": 1},
        "type": {"type": "string"},
        "status": {"type": "string", "enum": ["pending", "running", "completed", "failed", "cancelled"]},
        "runAfterUtc": {"type": "string", "format": "date-time"},
        "payloadJson": {"type": "string"},
        "resultJson": {"type": "string"},
        "errorMessage": {"type": ["string", "null"]},
        "startedAtUtc": {"type": ["string", "null"], "format": "date-time"},
        "completedAtUtc": {"type": ["string", "null"], "format": "date-time"},
        "userId": {"type": ["integer", "null"]},
        "correlationId": {"type": ["string", "null"]},
        "createdAtUtc": {"type": "string", "format": "date-time"},
        "updatedAtUtc": {"type": "string", "format": "date-time"},
        "logs": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "id": {"type": "integer", "minimum": 1},
              "level": {"type": "string", "enum": ["info", "warning", "error"]},
              "message": {"type": "string"},
              "dataJson": {"type": ["string", "null"]},
              "loggedAtUtc": {"type": "string", "format": "date-time"}
            },
            "required": ["id", "level", "message", "dataJson", "loggedAtUtc"],
            "additionalProperties": false
          }
        }
      },
      "required": ["id", "type", "status", "runAfterUtc", "payloadJson", "resultJson", "errorMessage", "startedAtUtc", "completedAtUtc", "userId", "correlationId", "createdAtUtc", "updatedAtUtc", "logs"],
      "additionalProperties": false
    }
    """;

}
