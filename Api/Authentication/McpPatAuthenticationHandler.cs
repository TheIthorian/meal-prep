using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Authentication;

/// <summary>
///     Authenticates MCP requests that include a personal access token as the first path segment after <c>/mcp/</c>.
/// </summary>
public sealed class McpPatAuthenticationHandler(
    IOptionsMonitor<McpPatAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory
)
    : AuthenticationHandler<McpPatAuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
        var path = Request.Path.Value ?? "";
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !segments[0].Equals("mcp", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var plain = segments[1];
        if (string.IsNullOrEmpty(plain)) return AuthenticateResult.NoResult();

        await using var scope = scopeFactory.CreateAsyncScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<McpPersonalAccessTokenService>();
        var validated = await tokenService.ValidateAndTouchAsync(plain, Context.RequestAborted);
        if (validated is null) return AuthenticateResult.Fail("Invalid or revoked MCP token.");

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, validated.User.Id.ToString()),
                new Claim(ClaimTypes.Name, validated.User.UserName ?? validated.User.Email ?? ""),
                new Claim(ClaimTypes.Email, validated.User.Email ?? ""),
                new Claim(McpPatClaims.WorkspaceId, validated.WorkspaceId.ToString()),
            ],
            McpPatAuthenticationDefaults.AuthenticationScheme
        );

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, McpPatAuthenticationDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }
}
