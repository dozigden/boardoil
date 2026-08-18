using BoardOil.Api.OAuth;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Endpoints;

public static class OAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods(
                "/connect/authorize",
                [HttpMethods.Get, HttpMethods.Post],
                HandleAuthorizationAsync)
            .WithTags("OAuth");

        app.MapPost("/connect/token", HandleTokenExchangeAsync)
            .WithTags("OAuth");

        app.MapPost("/connect/register", async (
                OAuthDynamicClientRegistrationRequest request,
                IOAuthDynamicClientRegistrationService registrationService,
                CancellationToken cancellationToken) =>
            {
                var result = await registrationService.RegisterAsync(request, cancellationToken);
                if (!result.Success || result.Registration is null)
                {
                    return Results.Json(result.Error, statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.Json(result.Registration, statusCode: StatusCodes.Status201Created);
            })
            .RequireRateLimiting(OAuthServiceCollectionExtensions.DynamicClientRegistrationRateLimitPolicy)
            .WithTags("OAuth");

        app.MapGet(OAuthResources.McpMetadataPath, async (
                HttpRequest request,
                IOAuthProtectedResourceMetadataService metadataService) =>
            Results.Json(await metadataService.GetMcpAsync(request)))
            .WithTags("OAuth");

        app.MapGet(OAuthResources.McpPath, async (
                HttpContext context,
                IOAuthProtectedResourceMetadataService metadataService,
                OAuthEndpointUrlResolver urlResolver) =>
            {
                var metadata = await metadataService.GetMcpAsync(context.Request);
                var metadataUrl = await urlResolver.ResolveAsync(
                    context.Request,
                    OAuthResources.McpMetadataPath);
                var scopes = string.Join(' ', metadata.ScopesSupported);
                context.Response.Headers.WWWAuthenticate =
                    $"Bearer resource_metadata=\"{metadataUrl}\", scope=\"{scopes}\"";
                return Results.StatusCode(StatusCodes.Status401Unauthorized);
            })
            .WithTags("OAuth");

        return app;
    }

    private static async Task<IResult> HandleAuthorizationAsync(
        HttpContext httpContext,
        OAuthAuthorizationService authorizationService,
        OAuthEndpointUrlResolver urlResolver,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth authorization request is unavailable.");
        ApplyInteractiveResponseHeaders(httpContext.Response);
        var loginEndpointUrl = await urlResolver.ResolveAsync(
            httpContext.Request,
            "/api/auth/login");
        var authorizationEndpointUrl = await urlResolver.ResolveAsync(
            httpContext.Request,
            "/connect/authorize");

        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Content(
                OAuthConsentPageRenderer.RenderLoginPage(
                    request,
                    loginEndpointUrl,
                    authorizationEndpointUrl),
                "text/html",
                System.Text.Encoding.UTF8);
        }

        var resolution = await authorizationService.ResolveAsync(
            request,
            httpContext.User,
            httpContext.Request,
            cancellationToken);
        if (!resolution.Success || resolution.Context is null)
        {
            return CreateProtocolForbid(
                resolution.Error ?? Errors.AccessDenied,
                resolution.ErrorDescription ?? "The authorization request was denied.");
        }

        if (HttpMethods.IsGet(httpContext.Request.Method))
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Content(
                OAuthConsentPageRenderer.RenderConsentPage(
                    request,
                    resolution.Context,
                    tokens,
                    authorizationEndpointUrl,
                    loginEndpointUrl),
                "text/html",
                System.Text.Encoding.UTF8);
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var hasConsentingUserId = int.TryParse(
            form["consenting_user_id"].ToString(),
            out var consentingUserId);

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
            when (hasConsentingUserId && consentingUserId != resolution.Context.UserId)
        {
            return RenderChangedConsentUser(
                httpContext,
                antiforgery,
                request,
                resolution.Context,
                authorizationEndpointUrl,
                loginEndpointUrl);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest("The OAuth consent form has expired. Restart the authorization flow.");
        }

        if (!hasConsentingUserId || consentingUserId != resolution.Context.UserId)
        {
            return RenderChangedConsentUser(
                httpContext,
                antiforgery,
                request,
                resolution.Context,
                authorizationEndpointUrl,
                loginEndpointUrl);
        }

        var decision = form["decision"].ToString();
        if (string.Equals(decision, "deny", StringComparison.Ordinal))
        {
            return CreateProtocolForbid(Errors.AccessDenied, "The user denied the authorization request.");
        }

        if (!string.Equals(decision, "approve", StringComparison.Ordinal))
        {
            return Results.BadRequest("An explicit approve or deny decision is required.");
        }

        var approvedScopes = form["approved_scope"]
            .Where(static scope => scope is not null)
            .Select(static scope => scope!)
            .ToArray();
        var approval = new OAuthAuthorizationApproval(
            form["connection_name"].ToString(),
            approvedScopes,
            string.Equals(form["replace_existing"].ToString(), "true", StringComparison.OrdinalIgnoreCase));
        var approvalResult = await authorizationService.ApproveAsync(
            resolution.Context,
            approval,
            cancellationToken);
        if (!approvalResult.Success || approvalResult.Principal is null)
        {
            var consentContext = resolution.Context;
            if (approvalResult.RequiresReplacementConfirmation
                && !string.IsNullOrWhiteSpace(approval.ConnectionName))
            {
                consentContext = consentContext with
                {
                    ExistingConnections =
                    [
                        .. consentContext.ExistingConnections,
                        approval.ConnectionName.Trim()
                    ]
                };
            }

            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Content(
                OAuthConsentPageRenderer.RenderConsentPage(
                    request,
                    consentContext,
                    tokens,
                    authorizationEndpointUrl,
                    loginEndpointUrl,
                    approval,
                    approvalResult.ErrorDescription),
                "text/html",
                System.Text.Encoding.UTF8,
                StatusCodes.Status400BadRequest);
        }

        return Results.SignIn(
            approvalResult.Principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult RenderChangedConsentUser(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        OpenIddictRequest request,
        OAuthAuthorizationContext authorizationContext,
        string authorizationEndpointUrl,
        string loginEndpointUrl)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Content(
            OAuthConsentPageRenderer.RenderConsentPage(
                request,
                authorizationContext,
                tokens,
                authorizationEndpointUrl,
                loginEndpointUrl,
                error: "The signed-in BoardOil user changed while consent was open. Review the current account and submit again."),
            "text/html",
            System.Text.Encoding.UTF8,
            StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> HandleTokenExchangeAsync(
        HttpContext httpContext,
        OAuthAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth token request is unavailable.");
        var authentication = await httpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return CreateProtocolForbid(Errors.InvalidGrant, "The authorization grant is invalid or expired.");
        }

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return CreateProtocolForbid(Errors.UnsupportedGrantType, "The grant type is not supported.");
        }

        var resolution = await authorizationService.RevalidateAsync(
            authentication.Principal,
            request,
            httpContext.Request,
            cancellationToken);
        if (!resolution.Success || resolution.Principal is null)
        {
            return CreateProtocolForbid(
                resolution.Error ?? Errors.InvalidGrant,
                resolution.ErrorDescription ?? "The authorization grant is no longer active.");
        }

        return Results.SignIn(
            resolution.Principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult CreateProtocolForbid(string error, string description)
    {
        var properties = new AuthenticationProperties();
        properties.SetString(OpenIddictServerAspNetCoreConstants.Properties.Error, error);
        properties.SetString(OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription, description);
        return Results.Forbid(
            properties,
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static void ApplyInteractiveResponseHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers.XFrameOptions = "DENY";
        response.Headers.XContentTypeOptions = "nosniff";
    }
}
