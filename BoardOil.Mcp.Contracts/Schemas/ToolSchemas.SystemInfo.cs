namespace BoardOil.Mcp.Contracts.Schemas;

public static partial class ToolSchemas
{
    public const string SystemInfoGetInput = """
    {
      "type": "object",
      "properties": {},
      "additionalProperties": false
    }
    """;

    public const string SystemInfoGetOutput = """
    {
      "type": "object",
      "properties": {
        "application": {
          "type": "object",
          "properties": {
            "version": {"type": "string"},
            "channel": {"type": "string"},
            "build": {"type": "string"},
            "commit": {"type": "string"}
          },
          "required": ["version", "channel", "build", "commit"],
          "additionalProperties": false
        },
        "database": {
          "type": "object",
          "properties": {
            "provider": {"type": "string"},
            "version": {"type": "string"},
            "journalMode": {"type": "string"},
            "sizeBytes": {"type": "integer", "minimum": 0, "description": "Allocated database size in bytes, as shown in System Support."}
          },
          "required": ["provider", "version", "journalMode", "sizeBytes"],
          "additionalProperties": false
        }
      },
      "required": ["application", "database"],
      "additionalProperties": false
    }
    """;
}
