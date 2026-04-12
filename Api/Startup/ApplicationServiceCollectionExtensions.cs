using Api.Configuration;
using Api.Endpoints.Requests;
using Api.Services;
using FluentValidation;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.AspNetCore.Diagnostics;

namespace Api.Startup;

/// <summary>
///     Registers the application's core services and integrations.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    private static int GetIntValue(
        IConfiguration configuration,
        string configKey,
        string envKey,
        int defaultValue
    ) {
        var value = configuration[configKey] ?? configuration[envKey];
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public static string[] GetHangfireQueues(IConfiguration configuration) {
        var queues = new List<string>();

        if (configuration.HasAppRole(AppRoles.WorkerCron))
            queues.Add(BackgroundJobQueues.Cron);

        if (configuration.HasAppRole(AppRoles.WorkerCleanup))
            queues.Add(BackgroundJobQueues.Cleanup);

        return queues.ToArray();
    }

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
                    // register filters here
                    return provider;
                }
            );

            services.AddAuthorization();

            services.AddHttpContextAccessor();
            services.AddScoped<CurrentUserService>();
            services.AddScoped<IS3StorageService, S3StorageService>();
            services.AddScoped<RetentionCleanupService>();
            services.AddScoped<CleanupBackgroundJobs>();
            services.AddOptions<RetentionCleanupOptions>()
                .Bind(configuration.GetSection("RetentionCleanup"))
                .Validate(
                    options => options.RetentionDays > 0,
                    "RetentionCleanup:RetentionDays must be greater than 0."
                )
                .ValidateOnStart();

            var redisConnectionString = configuration.GetConnectionString("Redis")
                                        ?? configuration["REDIS_CONNECTIONSTRING"];

            if (string.IsNullOrWhiteSpace(redisConnectionString))
                throw new InvalidOperationException(
                    "Redis connection string is required for Hangfire upload processing jobs."
                );

            services.AddStackExchangeRedisCache(options => { options.Configuration = redisConnectionString; });

            services.AddHangfire(config => {
                    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                        .UseSimpleAssemblyNameTypeSerializer()
                        .UseRecommendedSerializerSettings()
                        .UseRedisStorage(redisConnectionString, new RedisStorageOptions());
                }
            );

            var hangfireQueues = GetHangfireQueues(configuration);
            if (hangfireQueues.Length > 0)
                services.AddHangfireServer(options => {
                        options.Queues = hangfireQueues;
                        options.ShutdownTimeout = TimeSpan.FromMinutes(2);
                    }
                );
        }
    }
}
