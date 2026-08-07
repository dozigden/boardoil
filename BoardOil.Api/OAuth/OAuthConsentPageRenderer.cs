using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using OpenIddict.Abstractions;

namespace BoardOil.Api.OAuth;

internal static class OAuthConsentPageRenderer
{
    public static string RenderLoginPage(string loginEndpointUrl)
    {
        var content = $$"""
            <h1>Sign in to BoardOil</h1>
            <p>Use your administrator account to review this client connection.</p>
            <form id="login-form">
              <label>Username<input id="user-name" autocomplete="username" maxlength="64" required></label>
              <label>Password<input id="password" type="password" autocomplete="current-password" required></label>
              <p id="login-error" class="error" role="alert"></p>
              <button type="submit">Sign in</button>
            </form>
            <script>
              document.getElementById('login-form').addEventListener('submit', async event => {
                event.preventDefault();
                const error = document.getElementById('login-error');
                error.textContent = '';
                const response = await fetch({{JsonSerializer.Serialize(loginEndpointUrl)}}, {
                  method: 'POST',
                  credentials: 'same-origin',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({
                    userName: document.getElementById('user-name').value,
                    password: document.getElementById('password').value
                  })
                });
                if (response.ok) {
                  window.location.reload();
                  return;
                }

                let message = 'Sign in failed.';
                try {
                  const body = await response.json();
                  if (body.message) message = body.message;
                } catch {}
                error.textContent = message;
              });
            </script>
            """;
        return RenderDocument("Sign in", content);
    }

    public static string RenderConsentPage(
        OpenIddictRequest request,
        OAuthAuthorizationContext authorization,
        AntiforgeryTokenSet antiforgeryTokens,
        string authorizationEndpointUrl)
    {
        var builder = new StringBuilder();
        builder.Append("<h1>Authorise ")
            .Append(HtmlEncoder.Default.Encode(authorization.OAuthClientDisplayName))
            .Append("</h1>");
        builder.Append("<p>Review the exact identity and access this client is requesting.</p>");
        builder.Append("<dl>");
        AppendDetail(builder, "OAuth client", authorization.OAuthClientDisplayName);
        AppendDetail(
            builder,
            "Client account",
            $"{authorization.ClientAccountDisplayName} ({authorization.ClientAccountUserName})");
        AppendDetail(builder, "Project connection", authorization.ProjectConnectionName);
        AppendDetail(builder, "Resource", authorization.Resource, code: true);
        AppendDetail(builder, "Scopes", string.Join(' ', authorization.Scopes), code: true);
        builder.Append("</dl>");
        builder.Append("<p class=\"warning\">BoardOil will act as the client account above. Your own board access is not delegated.</p>");
        builder.Append("<form method=\"post\" action=\"")
            .Append(HtmlEncoder.Default.Encode(authorizationEndpointUrl))
            .Append("\">");
        AppendAuthorizationRequestFields(builder, request);
        AppendHiddenInput(
            builder,
            antiforgeryTokens.FormFieldName,
            antiforgeryTokens.RequestToken
                ?? throw new InvalidOperationException("The OAuth consent antiforgery token was not created."));
        builder.Append("<div class=\"actions\">");
        builder.Append("<button class=\"secondary\" type=\"submit\" name=\"decision\" value=\"deny\">Deny</button>");
        builder.Append("<button type=\"submit\" name=\"decision\" value=\"approve\">Authorise</button>");
        builder.Append("</div></form>");
        return RenderDocument(
            $"Authorise {authorization.OAuthClientDisplayName}",
            builder.ToString());
    }

    private static void AppendAuthorizationRequestFields(StringBuilder builder, OpenIddictRequest request)
    {
        AppendHiddenInput(builder, "client_id", request.ClientId);
        AppendHiddenInput(builder, "redirect_uri", request.RedirectUri);
        AppendHiddenInput(builder, "response_type", request.ResponseType);
        AppendHiddenInput(builder, "scope", request.Scope);
        AppendHiddenInput(builder, "state", request.State);
        AppendHiddenInput(builder, "code_challenge", request.CodeChallenge);
        AppendHiddenInput(builder, "code_challenge_method", request.CodeChallengeMethod);
        AppendHiddenInput(builder, "prompt", request.Prompt);
        AppendHiddenInput(builder, "nonce", request.Nonce);
        foreach (var resource in request.GetResources())
        {
            AppendHiddenInput(builder, "resource", resource);
        }
    }

    private static void AppendDetail(
        StringBuilder builder,
        string name,
        string value,
        bool code = false)
    {
        builder.Append("<dt>").Append(HtmlEncoder.Default.Encode(name)).Append("</dt><dd>");
        if (code)
        {
            builder.Append("<code>");
        }

        builder.Append(HtmlEncoder.Default.Encode(value));
        if (code)
        {
            builder.Append("</code>");
        }

        builder.Append("</dd>");
    }

    private static void AppendHiddenInput(StringBuilder builder, string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        builder.Append("<input type=\"hidden\" name=\"")
            .Append(HtmlEncoder.Default.Encode(name))
            .Append("\" value=\"")
            .Append(HtmlEncoder.Default.Encode(value))
            .Append("\">");
    }

    private static string RenderDocument(string title, string content) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="referrer" content="no-referrer">
          <title>{{HtmlEncoder.Default.Encode(title)}} · BoardOil</title>
          <style>
            :root { color-scheme: light dark; font-family: system-ui, sans-serif; }
            body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #17151b; color: #f6f5f4; }
            main { box-sizing: border-box; width: min(36rem, calc(100% - 2rem)); padding: 2rem; border: 1px solid #514b57; border-radius: .8rem; background: #241f2b; }
            h1 { margin-top: 0; }
            label { display: grid; gap: .35rem; margin: 1rem 0; font-weight: 650; }
            input { box-sizing: border-box; width: 100%; padding: .65rem; border: 1px solid #77717d; border-radius: .4rem; font: inherit; }
            dl { display: grid; grid-template-columns: max-content 1fr; gap: .8rem 1rem; margin: 1.5rem 0; }
            dt { color: #c0bfbc; font-weight: 650; }
            dd { margin: 0; min-width: 0; overflow-wrap: anywhere; }
            code { font-size: .9em; }
            button { padding: .65rem 1rem; border: 0; border-radius: .4rem; background: #3584e4; color: white; cursor: pointer; font: inherit; font-weight: 700; }
            button.secondary { background: #514b57; }
            .actions { display: flex; justify-content: flex-end; gap: .75rem; margin-top: 1.5rem; }
            .warning { padding: .8rem; border-left: .25rem solid #e5a50a; background: #332c1c; }
            .error { min-height: 1.25rem; color: #ff7b63; }
          </style>
        </head>
        <body><main>{{content}}</main></body>
        </html>
        """;
}
