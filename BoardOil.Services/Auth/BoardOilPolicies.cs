namespace BoardOil.Services.Auth;

public static class BoardOilPolicies
{
    public const string AdminOnly = "boardoil.admin";
    public const string AuthenticatedUser = "boardoil.authenticated";
    public const string McpAuthenticated = "boardoil.mcp-authenticated";
    public const string CardEditor = "boardoil.card-editor";
}
