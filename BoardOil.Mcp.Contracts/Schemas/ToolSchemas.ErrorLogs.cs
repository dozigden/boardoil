namespace BoardOil.Mcp.Contracts.Schemas;

public static partial class ToolSchemas
{
    public const string ErrorLogListInput = """
    {
      "type": "object",
      "properties": {
        "offset": {"type": "integer", "minimum": 0, "default": 0},
        "limit": {"type": "integer", "minimum": 1, "maximum": 200, "default": 100},
        "fromUtc": {"type": "string", "format": "date-time", "description": "Inclusive lower bound; include a UTC offset."},
        "toUtc": {"type": "string", "format": "date-time", "description": "Exclusive upper bound; include a UTC offset."},
        "source": {"type": "string", "maxLength": 32},
        "area": {"type": "string", "maxLength": 64},
        "message": {"type": "string", "maxLength": 2048},
        "traceIdentifier": {"type": "string", "maxLength": 128}
      },
      "additionalProperties": false
    }
    """;

    public const string ErrorLogGetInput = """
    {
      "type": "object",
      "properties": {
        "id": {"type": "integer", "minimum": 1}
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """;

    public const string ErrorLogListOutput = """
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
              "occurredAtUtc": {"type": "string", "format": "date-time"},
              "source": {"type": "string"},
              "area": {"type": "string"},
              "exceptionType": {"type": "string"},
              "message": {"type": "string"},
              "traceIdentifier": {"type": ["string", "null"]},
              "requestMethod": {"type": ["string", "null"]},
              "requestPath": {"type": ["string", "null"]},
              "actorUserId": {"type": ["integer", "null"]},
              "createdAtUtc": {"type": "string", "format": "date-time"},
              "updatedAtUtc": {"type": "string", "format": "date-time"},
              "jobId": {"type": ["integer", "null"]}
            },
            "required": ["id", "occurredAtUtc", "source", "area", "exceptionType", "message", "traceIdentifier", "requestMethod", "requestPath", "actorUserId", "createdAtUtc", "updatedAtUtc", "jobId"],
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

    public const string ErrorLogGetOutput = """
    {
      "type": "object",
      "properties": {
        "id": {"type": "integer", "minimum": 1},
        "occurredAtUtc": {"type": "string", "format": "date-time"},
        "source": {"type": "string"},
        "area": {"type": "string"},
        "exceptionType": {"type": "string"},
        "message": {"type": "string"},
        "traceIdentifier": {"type": ["string", "null"]},
        "requestMethod": {"type": ["string", "null"]},
        "requestPath": {"type": ["string", "null"]},
        "actorUserId": {"type": ["integer", "null"]},
        "createdAtUtc": {"type": "string", "format": "date-time"},
        "updatedAtUtc": {"type": "string", "format": "date-time"},
        "jobId": {"type": ["integer", "null"]},
        "stackTrace": {"type": ["string", "null"]},
        "contextJson": {"type": ["string", "null"]}
      },
      "required": ["id", "occurredAtUtc", "source", "area", "exceptionType", "message", "traceIdentifier", "requestMethod", "requestPath", "actorUserId", "createdAtUtc", "updatedAtUtc", "jobId", "stackTrace", "contextJson"],
      "additionalProperties": false
    }
    """;

}
