namespace BoardOil.Contracts.Mcp;

public sealed record McpProjectConnectionDto(
    int Id,
    string PublicId,
    string Name,
    int ClientAccountId,
    string ClientAccountUserName,
    string ClientAccountDisplayName,
    IReadOnlyList<string> AllowedScopes,
    string ResourceUrl,
    bool IsActive,
    int? CreatedByUserId,
    string CreatedByUserName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? RevokedAtUtc,
    int? RevokedByUserId,
    string? RevokedByUserName);

public sealed record CreateMcpProjectConnectionRequest(
    int ClientAccountId,
    string Name,
    string[] AllowedScopes);
