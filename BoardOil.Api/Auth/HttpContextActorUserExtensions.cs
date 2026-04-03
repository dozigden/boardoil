namespace BoardOil.Api.Auth;

internal static class HttpContextActorUserExtensions
{
    internal const string ActorUserIdItemKey = "__actorUserId";

    public static int GetActorUserId(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(ActorUserIdItemKey, out var value) && value is int actorUserId)
        {
            return actorUserId;
        }

        throw new InvalidOperationException("Actor user id was not found on the request context. Ensure RequireActorUserIdFilter is applied.");
    }

    public static void SetActorUserId(this HttpContext httpContext, int actorUserId) =>
        httpContext.Items[ActorUserIdItemKey] = actorUserId;
}
