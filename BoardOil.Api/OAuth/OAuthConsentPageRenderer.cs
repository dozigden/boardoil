using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using OpenIddict.Abstractions;

namespace BoardOil.Api.OAuth;

internal static class OAuthConsentPageRenderer
{
    private static readonly JsonSerializerOptions ScriptJsonOptions = new(JsonSerializerDefaults.Web);

    public static string RenderLoginPage(
        OpenIddictRequest request,
        string loginEndpointUrl,
        string authorizationEndpointUrl)
    {
        var builder = new StringBuilder();
        builder.Append("<h1>Sign in to BoardOil</h1>");
        builder.Append("<p>Sign in as the BoardOil user this connection should act as.</p>");
        AppendLoginForm(
            builder,
            loginEndpointUrl,
            BuildAuthorizationRequestUrl(request, authorizationEndpointUrl),
            "Sign in");
        return RenderDocument("Sign in", builder.ToString());
    }

    public static string RenderConsentPage(
        OpenIddictRequest request,
        OAuthAuthorizationContext authorization,
        AntiforgeryTokenSet antiforgeryTokens,
        string authorizationEndpointUrl,
        string loginEndpointUrl,
        OAuthAuthorizationApproval? approval = null,
        string? error = null)
    {
        var builder = new StringBuilder();
        builder.Append("<h1>Authorise ")
            .Append(HtmlEncoder.Default.Encode(authorization.OAuthClientDisplayName))
            .Append("</h1>");
        builder.Append("<p>Review the access this installation is requesting for your BoardOil account.</p>");
        builder.Append("<dl>");
        AppendDetail(builder, "OAuth application", authorization.OAuthClientDisplayName);
        AppendDetail(builder, "Redirect URI", request.RedirectUri ?? string.Empty, code: true);
        AppendDetail(builder, "Resource", authorization.Resource, code: true);
        AppendDetail(
            builder,
            "BoardOil user",
            $"{authorization.UserDisplayName} (@{authorization.UserName})");
        builder.Append("</dl>");
        builder.Append("<details class=\"account-switch\"><summary>Sign in as another user</summary>");
        builder.Append("<p>Switching account also changes the BoardOil user signed into this browser.</p>");
        AppendLoginForm(
            builder,
            loginEndpointUrl,
            BuildAuthorizationRequestUrl(request, authorizationEndpointUrl),
            "Switch account");
        builder.Append("</details>");
        builder.Append("<form method=\"post\" action=\"")
            .Append(HtmlEncoder.Default.Encode(authorizationEndpointUrl))
            .Append("\">");
        AppendAuthorizationRequestFields(builder, request);
        AppendHiddenInput(
            builder,
            antiforgeryTokens.FormFieldName,
            antiforgeryTokens.RequestToken
                ?? throw new InvalidOperationException("The OAuth consent antiforgery token was not created."));
        AppendHiddenInput(builder, "consenting_user_id", authorization.UserId.ToString());

        builder.Append("<label>Connection name<input id=\"connection-name\" name=\"connection_name\" maxlength=\"120\" required value=\"")
            .Append(HtmlEncoder.Default.Encode(approval?.ConnectionName ?? string.Empty))
            .Append("\"></label>");

        builder.Append("<fieldset><legend>Approved scopes</legend>");
        var approvedScopes = approval?.ApprovedScopes ?? authorization.RequestedScopes;
        foreach (var scope in authorization.RequestedScopes)
        {
            builder.Append("<label class=\"check-label\"><input type=\"checkbox\" name=\"approved_scope\" value=\"")
                .Append(HtmlEncoder.Default.Encode(scope))
                .Append('"');
            if (approvedScopes.Contains(scope, StringComparer.Ordinal))
            {
                builder.Append(" checked");
            }

            builder.Append("><code>")
                .Append(HtmlEncoder.Default.Encode(scope))
                .Append("</code></label>");
        }

        builder.Append("</fieldset>");
        builder.Append("<p class=\"warning\">This connection will act as your signed-in BoardOil user and can only access your current board memberships.</p>");
        builder.Append("<label id=\"replacement-warning\" class=\"replacement-warning\" hidden>")
            .Append("<input id=\"replace-existing\" type=\"checkbox\" name=\"replace_existing\" value=\"true\"");
        if (approval?.ReplaceExisting == true)
        {
            builder.Append(" checked");
        }

        builder.Append("><span>A connection with this name already exists. Revoke its previous authorization and replace it.</span></label>");
        builder.Append("<p class=\"error\" role=\"alert\">")
            .Append(HtmlEncoder.Default.Encode(error ?? string.Empty))
            .Append("</p>");
        builder.Append("<div class=\"actions\">");
        builder.Append("<button class=\"secondary\" type=\"submit\" name=\"decision\" value=\"deny\">Deny</button>");
        builder.Append("<button type=\"submit\" name=\"decision\" value=\"approve\">Authorise</button>");
        builder.Append("</div></form>");
        AppendReplacementScript(builder, authorization.ExistingConnections);
        return RenderDocument(
            $"Authorise {authorization.OAuthClientDisplayName}",
            builder.ToString());
    }

    private static void AppendLoginForm(
        StringBuilder builder,
        string loginEndpointUrl,
        string authorizationRequestUrl,
        string submitLabel)
    {
        builder.Append("<form id=\"login-form\">");
        builder.Append("<label>Username<input id=\"user-name\" autocomplete=\"username\" maxlength=\"64\" required></label>");
        builder.Append("<label>Password<input id=\"password\" type=\"password\" autocomplete=\"current-password\" required></label>");
        builder.Append("<p id=\"login-error\" class=\"error\" role=\"alert\"></p>");
        builder.Append("<button type=\"submit\">")
            .Append(HtmlEncoder.Default.Encode(submitLabel))
            .Append("</button></form>");
        builder.Append($$"""
            <script>
              document.getElementById('login-form').addEventListener('submit', async event => {
                event.preventDefault();
                const error = document.getElementById('login-error');
                error.textContent = '';
                try {
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
                    window.location.replace({{JsonSerializer.Serialize(authorizationRequestUrl)}});
                    return;
                  }

                  let message = 'Sign in failed.';
                  try {
                    const body = await response.json();
                    if (body.message) message = body.message;
                  } catch {}
                  error.textContent = message;
                } catch {
                  error.textContent = 'Sign in failed.';
                }
              });
            </script>
            """);
    }

    private static void AppendReplacementScript(
        StringBuilder builder,
        IReadOnlyList<string> existingConnections)
    {
        builder.Append("<script>");
        builder.Append("const existingConnections=")
            .Append(JsonSerializer.Serialize(existingConnections, ScriptJsonOptions))
            .Append(';');
        builder.Append("""
            const nameInput=document.getElementById('connection-name');
            const warning=document.getElementById('replacement-warning');
            const replacement=document.getElementById('replace-existing');
            function updateReplacementWarning(){
              const name=nameInput.value.trim().toUpperCase();
              const exists=existingConnections.some(item => item.trim().toUpperCase()===name);
              warning.hidden=!exists;
              replacement.required=exists;
              if(!exists) replacement.checked=false;
            }
            nameInput.addEventListener('input',updateReplacementWarning);
            updateReplacementWarning();
            </script>
            """);
    }

    private static void AppendAuthorizationRequestFields(StringBuilder builder, OpenIddictRequest request)
    {
        foreach (var field in GetAuthorizationRequestFields(request))
        {
            AppendHiddenInput(builder, field.Key, field.Value);
        }
    }

    private static string BuildAuthorizationRequestUrl(
        OpenIddictRequest request,
        string authorizationEndpointUrl)
    {
        var query = string.Join(
            '&',
            GetAuthorizationRequestFields(request)
                .Select(field => $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(field.Value)}"));
        if (string.IsNullOrEmpty(query))
        {
            return authorizationEndpointUrl;
        }

        var separator = authorizationEndpointUrl.Contains('?') ? '&' : '?';
        return $"{authorizationEndpointUrl}{separator}{query}";
    }

    private static IReadOnlyList<KeyValuePair<string, string>> GetAuthorizationRequestFields(
        OpenIddictRequest request)
    {
        var fields = new List<KeyValuePair<string, string>>();
        AddAuthorizationRequestField(fields, "client_id", request.ClientId);
        AddAuthorizationRequestField(fields, "redirect_uri", request.RedirectUri);
        AddAuthorizationRequestField(fields, "response_type", request.ResponseType);
        AddAuthorizationRequestField(fields, "scope", request.Scope);
        AddAuthorizationRequestField(fields, "state", request.State);
        AddAuthorizationRequestField(fields, "code_challenge", request.CodeChallenge);
        AddAuthorizationRequestField(fields, "code_challenge_method", request.CodeChallengeMethod);
        AddAuthorizationRequestField(fields, "prompt", request.Prompt);
        AddAuthorizationRequestField(fields, "nonce", request.Nonce);
        foreach (var resource in request.GetResources())
        {
            fields.Add(new KeyValuePair<string, string>("resource", resource));
        }

        return fields;
    }

    private static void AddAuthorizationRequestField(
        ICollection<KeyValuePair<string, string>> fields,
        string name,
        string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            fields.Add(new KeyValuePair<string, string>(name, value));
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
            main { box-sizing: border-box; width: min(38rem, calc(100% - 2rem)); padding: 2rem; border: 1px solid #514b57; border-radius: .8rem; background: #241f2b; }
            h1 { margin-top: 0; }
            label { display: grid; gap: .35rem; margin: 1rem 0; font-weight: 650; }
            input, select { box-sizing: border-box; width: 100%; padding: .65rem; border: 1px solid #77717d; border-radius: .4rem; font: inherit; }
            fieldset { display: grid; gap: .2rem; margin: 1rem 0; border: 1px solid #514b57; border-radius: .4rem; }
            .check-label { display: flex; align-items: center; gap: .5rem; margin: .4rem 0; }
            .check-label input, .replacement-warning input { width: auto; }
            dl { display: grid; grid-template-columns: max-content 1fr; gap: .8rem 1rem; margin: 1.5rem 0; }
            dt { color: #c0bfbc; font-weight: 650; }
            dd { margin: 0; min-width: 0; overflow-wrap: anywhere; }
            code { font-size: .9em; }
            button { padding: .65rem 1rem; border: 0; border-radius: .4rem; background: #3584e4; color: white; cursor: pointer; font: inherit; font-weight: 700; }
            button.secondary { background: #514b57; }
            .actions { display: flex; justify-content: flex-end; gap: .75rem; margin-top: 1.5rem; }
            .account-switch { margin: 1rem 0; padding: .8rem; border: 1px solid #514b57; border-radius: .4rem; }
            .account-switch summary { cursor: pointer; font-weight: 700; }
            .account-switch > p { color: #c0bfbc; }
            .warning, .replacement-warning { padding: .8rem; border-left: .25rem solid #e5a50a; background: #332c1c; }
            .replacement-warning { display: flex; grid-template-columns: auto 1fr; align-items: start; gap: .65rem; }
            .replacement-warning[hidden] { display: none; }
            .error { min-height: 1.25rem; color: #ff7b63; }
          </style>
        </head>
        <body><main>{{content}}</main></body>
        </html>
        """;
}
