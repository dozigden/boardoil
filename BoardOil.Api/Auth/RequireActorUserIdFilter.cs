using BoardOil.Api.Extensions;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Api.Auth;

internal sealed class RequireActorUserIdFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.User.TryGetUserId(out var actorUserId))
        {
            return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
        }

        context.HttpContext.SetActorUserId(actorUserId);
        return await next(context);
    }
}
