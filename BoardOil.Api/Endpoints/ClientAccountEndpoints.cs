using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class ClientAccountEndpoints
{
    public static IEndpointRouteBuilder MapClientAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/client-accounts", (IClientAccountService clientAccountService) =>
                clientAccountService.GetClientAccountsAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Client Accounts");

        app.MapPost("/api/system/client-accounts", (CreateClientAccountRequest request, IClientAccountService clientAccountService) =>
                clientAccountService.CreateClientAccountAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Client Accounts");
        app.MapPut("/api/system/client-accounts/{id:int}", (int id, UpdateClientAccountRequest request, IClientAccountService clientAccountService) =>
                clientAccountService.UpdateClientAccountAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Client Accounts");

        app.MapGet("/api/system/client-accounts/{id:int}/tokens", (int id, IClientAccountService clientAccountService) =>
                clientAccountService.ListClientAccessTokensAsync(id).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Client Accounts");

        app.MapPost("/api/system/client-accounts/{id:int}/tokens", (int id, CreateClientAccessTokenRequest request, IClientAccountService clientAccountService) =>
                clientAccountService.CreateClientAccessTokenAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Client Accounts");

        app.MapDelete("/api/system/client-accounts/{id:int}", (int id, IClientAccountService clientAccountService) =>
                clientAccountService.DeleteClientAccountAsync(id).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Client Accounts");

        app.MapDelete("/api/system/client-accounts/{id:int}/tokens/{tokenId:int}", (int id, int tokenId, IClientAccountService clientAccountService) =>
                clientAccountService.RevokeClientAccessTokenAsync(id, tokenId).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Client Accounts");

        return app;
    }
}
