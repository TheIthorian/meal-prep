using Api.Authentication;
using Api.Configuration;
using Api.Links;
using Api.Endpoints.Requests;
using Api.Services;
using Api.Services.MealPrep;
using Api.Services.UserSync;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;

namespace Api.Startup;

/// <summary>
///     Registers the application's core services and integrations.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    private static readonly TimeSpan RecipeImportHttpTimeout = TimeSpan.FromSeconds(10);

    extension(IServiceCollection services)
    {
        public void AddApplicationServices(IConfiguration configuration) {
            configuration.ValidateAppRolesConfiguration();

            services.AddOptions();
            services.AddValidatorsFromAssemblyContaining<PostWorkspaceRequestValidator>();
            services.AddOptions<AppRolesOptions>()
                .Configure(options => { options.Roles = configuration.GetAppRoles(); });
            services.AddOptions<S3StorageConfiguration>()
                .Bind(configuration.GetSection("S3"));
            services.AddOptions<OpenAIConfiguration>()
                .Bind(configuration.GetSection("OpenAI"));
            services.AddOptions<UserSyncOptions>()
                .Bind(configuration.GetSection(UserSyncOptions.SectionName));
            services.AddOptions<WebAppOptions>()
                .Bind(configuration.GetSection(WebAppOptions.SectionName))
                .PostConfigure(options => options.Normalize(configuration["WEB_APP_BASE_URL"]))
                .Validate(options => options.IsValid(), WebAppOptions.RequiredMessage)
                .ValidateOnStart();
            services.AddSingleton<WebAppLinkGenerator>();
            services.AddSingleton<ResourceLinkGenerator>();
            services.AddSingleton<RecipeLinkService>();
            services.AddProblemDetails();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.Configure<FormOptions>(options => {
                    options.MultipartBodyLengthLimit = RecipeImageUploadConstants.MaxBytes;
                }
            );
            services.AddAppRateLimiting();

            services.AddSingleton<IFilterConfigurationProvider>(_ => {
                    var provider = new FilterConfigurationProvider();
                    RecipeFilterConfigRegistration.RegisterRecipeFilters(provider);
                    return provider;
                }
            );

            services.AddAuthorization(options => {
                    options.AddPolicy(
                        McpAuthorizationPolicies.McpPat,
                        policy => {
                            policy.AuthenticationSchemes.Add(McpPatAuthenticationDefaults.AuthenticationScheme);
                            policy.RequireAuthenticatedUser();
                        }
                    );
                }
            );

            services.AddHttpContextAccessor();
            services.AddScoped<CurrentUserService>();
            services.AddScoped<McpPersonalAccessTokenService>();
            services.AddScoped<IS3StorageService, S3StorageService>();
            services.AddScoped<MeasurementService>();
            services.AddScoped<RecipeImageProcessingService>();
            services.AddScoped<RecipeImageStore>();
            services.AddScoped<RecipeDocumentImportService>();
            services.AddScoped<IIngredientCategoryResolver, IngredientCategoryResolutionService>();
            services.AddScoped<ShoppingListGenerationService>();
            services.AddSingleton<RecipeImportLlmParser>();
            services.AddSingleton<IngredientCategoryLlmService>();
            services.AddSingleton<RecipeTagSuggestionService>();
            // Import fetches happen while a user waits on an interactive import, so an unbounded wait
            // is a server-load problem as much as a UX one: the default 100s timeout would pin a
            // request thread and a connection per slow source page, and a handful of bad URLs is
            // then enough to exhaust the server. 10s is well past a healthy page fetch.
            // Sits inside a user's sign-in request, so the timeout is short and configurable: the sync
            // failing is always preferable to the sign-in being slow. UserSyncClient swallows the
            // resulting TaskCanceledException like every other failure.
            services.AddHttpClient<IUserSyncClient, UserSyncClient>()
                .ConfigureHttpClient((provider, client) => {
                        var options = provider.GetRequiredService<IOptions<UserSyncOptions>>().Value;
                        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
                    }
                );

            services.AddHttpClient<RecipeImportService>()
                .ConfigureHttpClient(client => client.Timeout = RecipeImportHttpTimeout)
                // Recipe pages are large and highly compressible (bbcgoodfood: 577 KB raw vs 88 KB
                // compressed), and the whole body is buffered into a string, so negotiating an
                // encoding cuts both transfer and large-object-heap churn. PreviewAsync sets its own
                // per-request User-Agent, so no default one is needed here, and redirects stay
                // enabled because the page-fetch path relies on following them.
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
                        AutomaticDecompression = DecompressionMethods.Brotli
                                                 | DecompressionMethods.GZip
                                                 | DecompressionMethods.Deflate,
                    }
                );
            services.AddHttpClient(RecipeImportService.RecipeImageImportHttpClientName)
                .ConfigureHttpClient(client => {
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("MealPrepBot/1.0");
                        client.Timeout = RecipeImportHttpTimeout;
                    }
                )
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
                        AllowAutoRedirect = false,
                        AutomaticDecompression = DecompressionMethods.Brotli
                                                 | DecompressionMethods.GZip
                                                 | DecompressionMethods.Deflate,
                    }
                );
        }
    }
}
