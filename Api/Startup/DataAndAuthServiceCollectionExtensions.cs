using Api.Authentication;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;

namespace Api.Startup;

/// <summary>
///     Registers data access, identity, and authentication services.
/// </summary>
public static class DataAndAuthServiceCollectionExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public void AddAppDatabase() {
            const int dbTimeout = 60;

            builder.Services.AddDbContext<ApiDbContext>((_, options) => {
                    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                                           ?? builder.Configuration["POSTGRES_CONNECTIONSTRING"];

                    if (string.IsNullOrEmpty(connectionString))
                        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                    var connBuilder = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = dbTimeout };
                    options.AddInterceptors(new TimestampInterceptor());
                    options.UseNpgsql(connBuilder.ToString(), o => o.CommandTimeout(dbTimeout));
                }
            );
        }

        public void AddAuthStateStorage() {
            var authStateStoreProvider = builder.Configuration["AuthStateStore:Provider"]?.Trim().ToLowerInvariant()
                                         ?? "redis";
            var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
                                        ?? builder.Configuration["REDIS_CONNECTIONSTRING"];

            var dataProtectionBuilder = builder.Services
                .AddDataProtection()
                .SetApplicationName("Api");

            switch (authStateStoreProvider) {
                case "redis":
                    if (string.IsNullOrWhiteSpace(redisConnectionString))
                        throw new InvalidOperationException(
                            "Redis auth state store was selected, but no Redis connection string was configured. Set 'ConnectionStrings__Redis'."
                        );

                    var redisMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
                    builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);
                    builder.Services.AddStackExchangeRedisCache(options => {
                            options.Configuration = redisConnectionString;
                        }
                    );
                    dataProtectionBuilder.PersistKeysToStackExchangeRedis(
                        redisMultiplexer,
                        "Api-DataProtection-Keys"
                    );
                    break;
                case "postgres":
                    builder.Services.AddDistributedMemoryCache();
                    dataProtectionBuilder.PersistKeysToDbContext<ApiDbContext>();
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported AuthStateStore:Provider value '{authStateStoreProvider}'. Supported values are 'Postgres' and 'Redis'."
                    );
            }
        }
    }

    extension(IServiceCollection services)
    {
        public void AddIdentityAndAuth(IHostEnvironment environment) {
            var isDevelopment = environment.IsDevelopment();
            var cookieSameSite = SameSiteMode.Lax;
            var cookieSecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

            services
                .AddIdentity<AppUser, IdentityRole<Guid>>(options => {
                        options.SignIn.RequireConfirmedAccount = false;
                        options.Password.RequiredLength = 8;
                        options.Password.RequireDigit = false;
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequireUppercase = false;
                        options.Password.RequireLowercase = false;
                        options.Lockout.AllowedForNewUsers = true;
                        options.Lockout.MaxFailedAccessAttempts = 5;
                        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    }
                )
                .AddEntityFrameworkStores<ApiDbContext>()
                .AddSignInManager()
                .AddApiEndpoints();

            services.ConfigureApplicationCookie(options => {
                    options.Cookie.Name = ".AspNetCore.Identity.Application";
                    options.Cookie.SameSite = cookieSameSite;
                    options.Cookie.SecurePolicy = cookieSecurePolicy;
                    options.SlidingExpiration = true;
                }
            );

            services.PostConfigure<AuthenticationOptions>(options => {
                    options.DefaultScheme = "Identity.Combined";
                    options.DefaultAuthenticateScheme = "Identity.Combined";
                    options.DefaultChallengeScheme = "Identity.Combined";
                }
            );

            services
                .AddAuthentication(options => { options.DefaultScheme = "Identity.Combined"; }
                )
                .AddPolicyScheme(
                    "Identity.Combined",
                    "Bearer or Cookie",
                    options => {
                        options.ForwardDefaultSelector = context => {
                            var hasBearerHeader = context.Request
                                .Headers
                                .Authorization
                                .ToString()
                                .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

                            return hasBearerHeader
                                ? IdentityConstants.BearerScheme
                                : IdentityConstants.ApplicationScheme;
                        };
                    }
                )
                .AddBearerToken(IdentityConstants.BearerScheme)
                .AddScheme<McpPatAuthenticationSchemeOptions, McpPatAuthenticationHandler>(
                    McpPatAuthenticationDefaults.AuthenticationScheme,
                    _ => { }
                );
        }

        public void AddFrontendCors(IConfiguration configuration) {
            services.AddCors(options => {
                    var allowedOrigins = configuration["CORS_ORIGINS"]
                        ?
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    options.AddPolicy(
                        "Frontend",
                        p => {
                            if (allowedOrigins is not null && allowedOrigins.Length > 0)
                                p.WithOrigins(allowedOrigins);
                            else
                                p.WithOrigins(
                                    "http://localhost:8080",
                                    "http://localhost:5000",
                                    "http://127.0.0.1:5500"
                                );

                            p.AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials();
                        }
                    );
                }
            );
        }
    }
}
