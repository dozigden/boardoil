namespace BoardOil.Api.Mcp;

internal static class McpOAuthChallengeState
{
    private static readonly object ItemKey = new();

    public static void MarkInvalidToken(HttpContext context) =>
        context.Items[ItemKey] = new McpOAuthChallenge(
            McpOAuthChallengeError.InvalidToken,
            RequiredScope: null);

    public static void MarkInsufficientScope(HttpContext context, string requiredScope) =>
        context.Items[ItemKey] = new McpOAuthChallenge(
            McpOAuthChallengeError.InsufficientScope,
            requiredScope);

    public static void Clear(HttpContext context) =>
        context.Items.Remove(ItemKey);

    public static bool TryGet(HttpContext context, out McpOAuthChallenge? challenge)
    {
        challenge = null;
        if (!context.Items.TryGetValue(ItemKey, out var value))
        {
            return false;
        }

        challenge = value as McpOAuthChallenge;
        return challenge is not null;
    }
}

internal sealed record McpOAuthChallenge(
    McpOAuthChallengeError Error,
    string? RequiredScope);

internal enum McpOAuthChallengeError
{
    InvalidToken,
    InsufficientScope,
}
