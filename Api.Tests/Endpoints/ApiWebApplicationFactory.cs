using Api.Data;
using Api.Models;
using Api.Services;
using Api.Tests.Infrastructure;
using Api.Tests.Integration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Tests.Endpoints;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestOrIdentityScheme = "TestOrIdentity";

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        AddDefaultForwardedForHeader(client);
        return client;
    }

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        var client = base.CreateClient(options);
        AddDefaultForwardedForHeader(client);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable("AppRoles", "api");
        Environment.SetEnvironmentVariable("AuthStateStore__Provider", "Postgres");
        Environment.SetEnvironmentVariable("AuthStateStore:Provider", "Postgres");
        Environment.SetEnvironmentVariable(
            "OpenAI__ApiKey",
            Environment.GetEnvironmentVariable("OpenAI__ApiKey") ?? "test-openai-key"
        );
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Redis",
            Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
            ?? Environment.GetEnvironmentVariable("REDIS_CONNECTIONSTRING")
            ?? "localhost:6379,abortConnect=false"
        );
        Environment.SetEnvironmentVariable(
            "REDIS_CONNECTIONSTRING",
            Environment.GetEnvironmentVariable("REDIS_CONNECTIONSTRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
            ?? "localhost:6379,abortConnect=false"
        );

        builder.ConfigureLogging(logging => {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Microsoft", LogLevel.Warning);
            logging.AddFilter("System", LogLevel.Warning);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        });

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var connectionString = TestEnvironment.GetDatabaseConnectionString();
            var config = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings__DefaultConnection"] = connectionString,
                ["POSTGRES_CONNECTIONSTRING"] = connectionString,
                ["AppRoles"] = "api",
                ["AuthStateStore:Provider"] = "Postgres",
                ["AuthStateStore__Provider"] = "Postgres",
                ["OpenAI:ApiKey"] = TestEnvironment.GetOpenAiApiKey(),
                ["OpenAI:BaseUrl"] = "https://api.openai.com/v1",
                ["OpenAI:Model"] = "gpt-4o-mini",
                ["Test:DisableOpenTelemetry"] = "true",
                ["S3:ServiceUrl"] = TestEnvironment.GetS3ServiceUrl(),
                ["S3:AccessKey"] = TestEnvironment.GetS3AccessKey(),
                ["S3:SecretKey"] = TestEnvironment.GetS3SecretKey(),
                ["S3:BucketName"] = TestEnvironment.GetS3Bucket(),
                ["S3:Region"] = TestEnvironment.GetS3Region(),
                ["CORS_ORIGINS"] = "http://localhost:8080,http://localhost:5000",
                ["Logging:LogLevel:Default"] = Environment.GetEnvironmentVariable("Logging__LogLevel__Default")
                                               ?? "Warning",
                ["Logging:LogLevel:Microsoft"] = Environment.GetEnvironmentVariable("Logging__LogLevel__Microsoft")
                                                 ?? "Warning",
                ["Logging:LogLevel:System"] = Environment.GetEnvironmentVariable("Logging__LogLevel__System")
                                              ?? "Warning"
            };

            configBuilder.AddInMemoryCollection(config);
        }
        );

        builder.ConfigureTestServices(services =>
        {
            services.AddMemoryCache();
            services.RemoveAll<IS3StorageService>();
            services.AddSingleton<IS3StorageService, InMemoryS3StorageService>();

            services.AddAuthentication()
                .AddPolicyScheme(
                    TestOrIdentityScheme,
                    "Test auth or identity",
                    options =>
                    {
                        options.ForwardDefaultSelector = context =>
                            context.Request.Headers.ContainsKey(TestAuthHandler.UserIdHeaderName)
                                ? TestAuthHandler.Scheme
                                : "Identity.Combined";
                    }
                )
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestOrIdentityScheme;
                options.DefaultChallengeScheme = TestOrIdentityScheme;
            }
            );
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthHandler.UserIdHeaderName, userId.ToString());
        return client;
    }

    public async Task<(Guid UserId, Guid WorkspaceId)> SeedUserWithWorkspaceAsync(string workspaceName)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var userId = Guid.NewGuid();
        var email = $"endpoint-{userId:N}@tests.local";

        var user = new AppUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        var workspace = Workspace.CreateNew(workspaceName);
        var membership = WorkspaceUser.CreateNew(user, workspace, WorkspaceUser.Roles.Owner);

        db.Users.Add(user);
        db.Workspaces.Add(workspace);
        db.WorkspaceUsers.Add(membership);
        await db.SaveChangesAsync();

        return (user.Id, workspace.Id);
    }

    public async Task<(Guid UserId, Guid WorkspaceId, string Email)> SeedIdentityUserWithWorkspaceAsync(
        string workspaceName,
        string password
    )
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var email = $"identity-{Guid.NewGuid():N}@tests.local";
        var user = new AppUser { UserName = email, Email = email, DisplayName = workspaceName };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create identity test user: {string.Join(", ", result.Errors.Select(e => e.Description))}"
            );

        var workspace = Workspace.CreateNew(workspaceName);
        var membership = WorkspaceUser.CreateNew(user, workspace, WorkspaceUser.Roles.Owner);

        db.Workspaces.Add(workspace);
        db.WorkspaceUsers.Add(membership);
        await db.SaveChangesAsync();

        return (user.Id, workspace.Id, email);
    }

    private sealed class InMemoryS3StorageService : IS3StorageService
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            using var memory = new MemoryStream();
            await fileStream.CopyToAsync(memory);

            var key = $"{Guid.NewGuid():N}_{fileName}";
            files[key] = memory.ToArray();
            return key;
        }

        public Task<Stream> DownloadFileAsync(string s3Key)
        {
            if (!files.TryGetValue(s3Key, out var payload))
                throw new InvalidOperationException($"Test file '{s3Key}' was not found.");

            Stream stream = new MemoryStream(payload, false);
            return Task.FromResult(stream);
        }

        public Task DeleteFileAsync(string s3Key)
        {
            files.Remove(s3Key);
            return Task.CompletedTask;
        }
    }
    private static void AddDefaultForwardedForHeader(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "127.0.0.1");
    }
}
