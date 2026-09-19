namespace BoardOil.Api.Auth;

internal enum JwtAuthenticationSource
{
    BearerHeader,
    Cookie
}

internal static class JwtAuthenticationSourceContext
{
    private static readonly object CandidateKey = new();
    private static readonly object ValidatedKey = new();

    public static void SetCandidate(HttpContext context, JwtAuthenticationSource source) =>
        context.Items[CandidateKey] = source;

    public static void ConfirmCandidate(HttpContext context)
    {
        if (context.Items.TryGetValue(CandidateKey, out var source))
        {
            context.Items[ValidatedKey] = source;
        }
    }

    public static bool IsValidatedBearerHeader(HttpContext context) =>
        context.Items.TryGetValue(ValidatedKey, out var source)
        && source is JwtAuthenticationSource.BearerHeader;
}
