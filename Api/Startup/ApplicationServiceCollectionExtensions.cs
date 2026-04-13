using Api.Authentication;
using Api.Configuration;
using Api.Endpoints.Requests;
using Api.Services;
using Api.Services.MealPrep;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using System.Net;
using System.Net.Http;

namespace Api.Startup;

/// <summary>
///     Registers the application's core services and integrations.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
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
            services.AddScoped<IIngredientCategoryResolver, IngredientCategoryResolutionService>();
            services.AddScoped<ShoppingListGenerationService>();
            services.AddSingleton<RecipeImportLlmParser>();
            services.AddSingleton<IngredientCategoryLlmService>();
            services.AddSingleton<RecipeTagSuggestionService>();
            services.AddHttpClient<RecipeImportService>();
            services.AddHttpClient(RecipeImportService.RecipeImageImportHttpClientName)
                .ConfigureHttpClient(client => client.DefaultRequestHeaders.UserAgent.ParseAdd("MealPrepBot/1.0"))
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
