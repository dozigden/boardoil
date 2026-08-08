namespace BoardOil.Contracts.OAuth;

public sealed record OAuthConnectionOwnerDto(
    int Id,
    string UserName,
    string DisplayName);

public sealed record OAuthConnectionDto(
    int Id,
    string Name,
    string ResourceType,
    OAuthConnectionOwnerDto Owner,
    IReadOnlyList<string> ApprovedScopes,
    string OAuthClientId,
    string OAuthClientDisplayName,
    string Resource,
    DateTime CreatedAtUtc,
    DateTime LastAuthorizedAtUtc,
    DateTime? LastUsedAtUtc);
