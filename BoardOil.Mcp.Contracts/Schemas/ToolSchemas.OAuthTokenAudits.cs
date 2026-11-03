namespace BoardOil.Mcp.Contracts.Schemas;

public static partial class ToolSchemas
{
    public const string OAuthTokenAuditListInput = """
    {
      "type": "object",
      "properties": {
        "offset": {"type": "integer", "minimum": 0, "default": 0},
        "limit": {"type": "integer", "minimum": 1, "maximum": 200, "default": 100},
        "fromUtc": {"type": "string", "format": "date-time", "description": "Inclusive lower bound on occurrence time; include a UTC offset."},
        "toUtc": {"type": "string", "format": "date-time", "description": "Inclusive upper bound on occurrence time; include a UTC offset."},
        "outcome": {"type": "string", "enum": ["Succeeded", "Rejected"]},
        "grantType": {"type": "string", "description": "Exact grant type, such as authorization_code or refresh_token."},
        "connectionId": {"type": "integer", "minimum": 1, "description": "Match oauthConnectionId from a returned audit event."},
        "clientId": {"type": "string", "description": "Exact oauthClientId from a returned audit event."},
        "authorizationId": {"type": "string", "description": "Exact authorization identifier."},
        "tokenFingerprint": {"type": "string", "description": "Exact sha256: fingerprint from a returned event. Matches either presentedTokenFingerprint or issuedRefreshTokenFingerprint. Never pass a raw token."}
      },
      "additionalProperties": false
    }
    """;

    public const string OAuthTokenAuditListOutput = """
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
              "outcome": {"type": "string"},
              "grantType": {"type": "string"},
              "requestedScopes": {"type": ["string", "null"]},
              "errorCode": {"type": ["string", "null"]},
              "presentedTokenFingerprint": {"type": ["string", "null"]},
              "issuedRefreshTokenFingerprint": {"type": ["string", "null"]},
              "authorizationId": {"type": ["string", "null"]},
              "oauthClientId": {"type": ["string", "null"]},
              "oauthConnectionId": {"type": ["integer", "null"]},
              "traceIdentifier": {"type": ["string", "null"]}
            },
            "required": ["id", "occurredAtUtc", "outcome", "grantType", "requestedScopes", "errorCode", "presentedTokenFingerprint", "issuedRefreshTokenFingerprint", "authorizationId", "oauthClientId", "oauthConnectionId", "traceIdentifier"],
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
}
