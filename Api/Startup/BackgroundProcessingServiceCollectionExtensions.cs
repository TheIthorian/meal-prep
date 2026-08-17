using Api.Services.MealPrep;

namespace Api.Startup;

/// <summary>
///     Registers the in-process background workers and the queues that feed them. Hosted services are
///     kept out of <see cref="ApplicationServiceCollectionExtensions" /> so request-path registrations
///     stay free of anything that starts running on its own.
/// </summary>
public static class BackgroundProcessingServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddBackgroundProcessing(IConfiguration configuration) {
            services.AddOptions<RecipeCollectionImportJobOptions>()
                .Bind(configuration.GetSection(RecipeCollectionImportJobOptions.SectionName));

            services.AddSingleton<RecipeCollectionImportJobQueue>();
            services.AddHostedService<RecipeCollectionImportJobWorker>();
        }
    }
}
