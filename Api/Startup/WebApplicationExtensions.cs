using Api.Configuration;
using Api.Data;
using Api.Endpoints.Middleware;

namespace Api.Startup;

/// <summary>
///     Provides application startup extensions for middleware, migrations, and recurring jobs.
/// </summary>
public static class WebApplicationExtensions
{
    extension(WebApplication app)
    {
        public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await DatabaseMigrationCoordinator.MigrateWithLockAsync(db, cancellationToken);
        }

        public void UseGlobalExceptionHandler()
        {
            app.UseExceptionHandler();
        }

        public void UseApiPipeline()
        {
            var cookieSameSite = app.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;
            var cookieSecurePolicy = app.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            app.UseCookiePolicy(
                new CookiePolicyOptions { MinimumSameSitePolicy = cookieSameSite, Secure = cookieSecurePolicy }
            );

            app.UseMiddleware<ForwardedForHeaderMiddleware>();
            app.UseMiddleware<RequestContextMiddleware>();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseWebSockets();
            app.UseMiddleware<AppTelemetryMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.MapSwagger();
                app.UseSwagger();
                app.UseSwaggerUI(options => {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Meal Prep API v1");
                });
            }
        }
    }
}
