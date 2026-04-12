using Api.Configuration;
using Api.Endpoints.Requests;
using Api.Services.MealPrep;
using Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

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
            services.AddProblemDetails();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddAppRateLimiting();

            services.AddSingleton<IFilterConfigurationProvider>(_ => {
                    var provider = new FilterConfigurationProvider();
                    RecipeFilterConfigRegistration.RegisterRecipeFilters(provider);
                    return provider;
                }
            );

            services.AddAuthorization();

            services.AddHttpContextAccessor();
            services.AddScoped<CurrentUserService>();
            services.AddScoped<IS3StorageService, S3StorageService>();
            services.AddScoped<MeasurementService>();
            services.AddScoped<ShoppingListGenerationService>();
            services.AddHttpClient<RecipeImportService>();
        }
    }
}
