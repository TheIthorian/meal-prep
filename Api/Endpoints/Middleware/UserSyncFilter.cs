using Api.Models;
using Api.Services.UserSync;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using RegisterRequest = Api.Endpoints.Requests.RegisterRequest;

namespace Api.Endpoints.Middleware;

/// <summary>
///     Pushes a user into the Node app (server B) after this API has successfully authenticated them, so that
///     server B holds a real Better Auth credential for them before the migration cuts over.
///     <para>
///         An endpoint filter is the only available seam for sign-in: <c>/login</c> comes from
///         <c>MapIdentityApi&lt;AppUser&gt;()</c>, not from a handler this repo owns, so there is no method to
///         edit. <see cref="SeedUserDataFilter" /> already established this pattern on the same group.
///     </para>
///     <para>
///         Runs only AFTER the wrapped endpoint has succeeded, so a wrong password is never forwarded anywhere.
///     </para>
///     <para>
///         The call is awaited rather than fired onto a background task. <see cref="UserSyncClient" /> already
///         guarantees it neither throws nor waits beyond its own short timeout, so awaiting costs a bounded few
///         milliseconds and keeps the work inside the request's lifetime - rather than leaking a task whose
///         failure nothing observes and whose scope may already have been disposed.
///     </para>
/// </summary>
public class UserSyncFilter(
    IUserSyncClient userSyncClient,
    UserManager<AppUser> userManager
) : IEndpointFilter
{
    private const string LoginEndpoint = "POST /api/v1/auth/login";
    private const string SignupEndpoint = "POST /api/v1/auth/signup";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        var result = await next(context);

        var displayName = context.HttpContext.GetEndpoint()?.DisplayName;
        if (displayName is null) return result;

        var isLogin = displayName.Contains(LoginEndpoint, StringComparison.Ordinal);
        var isSignup = displayName.Contains(SignupEndpoint, StringComparison.Ordinal);
        if (!isLogin && !isSignup) return result;

        if (!IsSuccess(context, result)) return result;

        var credentials = ExtractCredentials(context, isLogin);
        if (credentials is null) return result;

        var (email, password) = credentials.Value;

        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return result;

        await userSyncClient.SyncAsync(
            user.Id,
            user.Email ?? email,
            user.DisplayName,
            password,
            context.HttpContext.RequestAborted
        );

        return result;
    }

    /// <summary>
    ///     A failed sign-in must never forward the attempted password. Identity's login endpoint returns a
    ///     problem result for a bad credential and a signed-in result for a good one, so the response's status
    ///     code is the authoritative signal; when the result carries no status code, the response's own is used.
    /// </summary>
    private static bool IsSuccess(EndpointFilterInvocationContext context, object? result) {
        var statusCode = result switch {
            IStatusCodeHttpResult { StatusCode: not null } statusResult => statusResult.StatusCode.Value,
            _ => context.HttpContext.Response.StatusCode,
        };

        return statusCode is >= 200 and < 300;
    }

    /// <summary>
    ///     Login carries Identity's own <see cref="LoginRequest" />; signup carries this repo's
    ///     <see cref="RegisterRequest" />. Both expose the plaintext the user just proved they know.
    /// </summary>
    private static (string Email, string Password)? ExtractCredentials(
        EndpointFilterInvocationContext context,
        bool isLogin
    ) {
        if (isLogin) {
            var login = context.Arguments.OfType<LoginRequest>().FirstOrDefault();
            return login is null ? null : (login.Email, login.Password);
        }

        var register = context.Arguments.OfType<RegisterRequest>().FirstOrDefault();
        return register is null ? null : (register.Email, register.Password);
    }
}
