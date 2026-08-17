using System.Net;
using System.Net.Http.Json;
using System.Text;
using Api.Data;
using Api.Endpoints.Responses;
using Api.Endpoints.Responses.MealPrep;
using Api.Models;
using Api.Services;
using Api.Services.MealPrep;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Endpoints;

public sealed class RecipeCollectionImportJobEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public RecipeCollectionImportJobEndpointsTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Fact]
    public async Task PostImportJob_QueuesJobWithOneItemPerSourceRecipe() {
        var shared = await SeedSharedCollectionAsync("Import Job Queue", ["Soup", "Salad", "Stew"]);

        var response = await shared.Client.PostAsync(
            $"/api/v1/workspaces/{shared.TargetWorkspaceId}/recipe-collection-import/{shared.ShareToken}/jobs",
            EmptyJsonContent()
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var job = (await response.Content.ReadFromJsonAsync<RecipeCollectionImportJobResponse>())!;
        Assert.Equal(RecipeCollectionImportJobStatuses.Pending, job.Status);
        Assert.Equal(3, job.TotalRecipes);
        Assert.Equal(0, job.ProcessedRecipes);
        Assert.Equal(0, job.ImportedRecipes);
        Assert.Empty(job.Failures);
        Assert.Null(job.TargetCollectionId);
    }

    [Fact]
    public async Task ImportJob_ReportsProgressAndCompletesWithImportedRecipes() {
        var shared = await SeedSharedCollectionAsync("Import Job Progress", ["Soup", "Salad"]);
        var job = await StartImportJobAsync(shared);

        await RunJobAsync(job.Id);

        var finished = await GetJobAsync(shared, job.Id);
        Assert.Equal(RecipeCollectionImportJobStatuses.Completed, finished.Status);
        Assert.Equal(2, finished.TotalRecipes);
        Assert.Equal(2, finished.ProcessedRecipes);
        Assert.Equal(2, finished.ImportedRecipes);
        Assert.Equal(0, finished.FailedRecipes);
        Assert.Empty(finished.Failures);
        Assert.NotNull(finished.TargetCollectionId);
        Assert.NotNull(finished.CompletedAt);

        var collectionResponse = await shared.Client.GetAsync(
            $"/api/v1/workspaces/{shared.TargetWorkspaceId}/recipe-collections/{finished.TargetCollectionId}"
        );
        collectionResponse.EnsureSuccessStatusCode();

        var collection = (await collectionResponse.Content.ReadFromJsonAsync<RecipeCollectionDetailResponse>())!;
        Assert.Equal(2, collection.Recipes.Length);
    }

    [Fact]
    public async Task ImportJob_ListsFailedRecipesSeparatelyFromSuccessfulOnes() {
        var shared = await SeedSharedCollectionAsync("Import Job Failures", ["Soup", "Broken Salad"]);
        await BreakRecipeImageAsync(shared, "Broken Salad");

        var job = await StartImportJobAsync(shared);
        await RunJobAsync(job.Id);

        var finished = await GetJobAsync(shared, job.Id);
        Assert.Equal(RecipeCollectionImportJobStatuses.CompletedWithErrors, finished.Status);
        Assert.Equal(2, finished.TotalRecipes);
        Assert.Equal(2, finished.ProcessedRecipes);
        Assert.Equal(1, finished.ImportedRecipes);
        Assert.Equal(1, finished.FailedRecipes);

        var failure = Assert.Single(finished.Failures);
        Assert.Equal("Broken Salad", failure.RecipeTitle);
        Assert.False(string.IsNullOrWhiteSpace(failure.ErrorMessage));
    }

    [Fact]
    public async Task RetryImportJob_ReimportsOnlyTheFailedRecipes() {
        var shared = await SeedSharedCollectionAsync("Import Job Retry", ["Soup", "Broken Salad"]);
        var brokenImageKey = await BreakRecipeImageAsync(shared, "Broken Salad");

        var job = await StartImportJobAsync(shared);
        await RunJobAsync(job.Id);

        var failed = await GetJobAsync(shared, job.Id);
        Assert.Equal(RecipeCollectionImportJobStatuses.CompletedWithErrors, failed.Status);

        // Whatever made the image unreadable is fixed, so the retry should now go through.
        await RestoreImageAsync(brokenImageKey);

        var retryResponse = await shared.Client.PostAsync(
            $"/api/v1/workspaces/{shared.TargetWorkspaceId}/recipe-collection-import-jobs/{job.Id}/retry",
            EmptyJsonContent()
        );
        Assert.Equal(HttpStatusCode.Accepted, retryResponse.StatusCode);

        var requeued = (await retryResponse.Content.ReadFromJsonAsync<RecipeCollectionImportJobResponse>())!;
        Assert.Equal(RecipeCollectionImportJobStatuses.Pending, requeued.Status);
        Assert.Equal(1, requeued.ProcessedRecipes);
        Assert.Equal(1, requeued.ImportedRecipes);
        Assert.Empty(requeued.Failures);

        await RunJobAsync(job.Id);

        var completed = await GetJobAsync(shared, job.Id);
        Assert.Equal(RecipeCollectionImportJobStatuses.Completed, completed.Status);
        Assert.Equal(2, completed.ImportedRecipes);
        Assert.Empty(completed.Failures);
        Assert.Equal(failed.TargetCollectionId, completed.TargetCollectionId);

        var collectionResponse = await shared.Client.GetAsync(
            $"/api/v1/workspaces/{shared.TargetWorkspaceId}/recipe-collections/{completed.TargetCollectionId}"
        );
        collectionResponse.EnsureSuccessStatusCode();

        var collection = (await collectionResponse.Content.ReadFromJsonAsync<RecipeCollectionDetailResponse>())!;
        Assert.Equal(2, collection.Recipes.Length);
    }

    [Fact]
    public async Task RetryImportJob_RejectsJobWithNothingToRetry() {
        var shared = await SeedSharedCollectionAsync("Import Job Retry Guard", ["Soup"]);
        var job = await StartImportJobAsync(shared);
        await RunJobAsync(job.Id);

        var retryResponse = await shared.Client.PostAsync(
            $"/api/v1/workspaces/{shared.TargetWorkspaceId}/recipe-collection-import-jobs/{job.Id}/retry",
            EmptyJsonContent()
        );

        Assert.Equal(HttpStatusCode.BadRequest, retryResponse.StatusCode);
    }

    [Fact]
    public async Task GetImportJobs_FindsRunningJobByShareTokenAfterAReload() {
        var shared = await SeedSharedCollectionAsync("Import Job Recovery", ["Soup", "Salad"]);
        var job = await StartImportJobAsync(shared);

        // A fresh client stands in for the user coming back to the page with no client-side state.
        using var reloadedClient = factory.CreateAuthenticatedClient(shared.UserId);

        var listResponse = await reloadedClient.GetAsync(
            $"/api/v1/workspaces/{shared.TargetWorkspaceId}/recipe-collection-import-jobs?shareToken={shared.ShareToken}"
        );
        listResponse.EnsureSuccessStatusCode();

        var jobs = (await listResponse.Content.ReadFromJsonAsync<RecipeCollectionImportJobResponse[]>())!;
        var found = Assert.Single(jobs);
        Assert.Equal(job.Id, found.Id);
        Assert.Equal(2, found.TotalRecipes);
        Assert.Equal(shared.ShareToken, found.ShareToken);
    }

    [Fact]
    public async Task GetImportJob_IsNotVisibleFromAnotherWorkspace() {
        var shared = await SeedSharedCollectionAsync("Import Job Isolation", ["Soup"]);
        var job = await StartImportJobAsync(shared);

        var (otherUserId, otherWorkspaceId) = await factory.SeedUserWithWorkspaceAsync("Unrelated Workspace");
        using var otherClient = factory.CreateAuthenticatedClient(otherUserId);

        var response = await otherClient.GetAsync(
            $"/api/v1/workspaces/{otherWorkspaceId}/recipe-collection-import-jobs/{job.Id}"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<RecipeCollectionImportJobResponse> StartImportJobAsync(SharedCollection shared) {
        var response = await shared.Client.PostAsync(
            $"/api/v1/workspaces/{shared.TargetWorkspaceId}/recipe-collection-import/{shared.ShareToken}/jobs",
            EmptyJsonContent()
        );
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<RecipeCollectionImportJobResponse>())!;
    }

    private async Task<RecipeCollectionImportJobResponse> GetJobAsync(SharedCollection shared, Guid jobId) {
        var response = await shared.Client.GetAsync(
            $"/api/v1/workspaces/{shared.TargetWorkspaceId}/recipe-collection-import-jobs/{jobId}"
        );
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<RecipeCollectionImportJobResponse>())!;
    }

    private async Task RunJobAsync(Guid jobId) {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RecipeCollectionImportJobService>();
        await service.RunAsync(jobId, CancellationToken.None);
    }

    /// <summary>
    ///     Points a source recipe at an image object that does not exist, which makes cloning it throw.
    /// </summary>
    private async Task<string> BreakRecipeImageAsync(SharedCollection shared, string recipeTitle) {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var recipe = await db.Recipes.FirstAsync(
            value => value.WorkspaceId == shared.SourceWorkspaceId && value.Title == recipeTitle
        );

        var missingKey = $"missing-{Guid.NewGuid():N}.webp";
        recipe.SetImageObjectKey(missingKey);
        await db.SaveChangesAsync();

        return missingKey;
    }

    private async Task RestoreImageAsync(string objectKey) {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IS3StorageService>();

        using var payload = new MemoryStream("not-really-an-image"u8.ToArray());
        await storage.UploadFileAtKeyAsync(payload, objectKey, "image/webp");
    }

    private async Task<SharedCollection> SeedSharedCollectionAsync(string name, string[] recipeTitles) {
        var (userId, sourceWorkspaceId) = await factory.SeedUserWithWorkspaceAsync($"{name} Source");
        var client = factory.CreateAuthenticatedClient(userId);

        var targetWorkspaceResponse = await client.PostAsJsonAsync(
            "/api/v1/workspaces",
            new { name = $"{name} Target" }
        );
        targetWorkspaceResponse.EnsureSuccessStatusCode();
        var targetWorkspace = (await targetWorkspaceResponse.Content.ReadFromJsonAsync<WorkspaceResponse>())!;

        var collectionResponse = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{sourceWorkspaceId}/recipe-collections",
            new { name, description = "Shared for import" }
        );
        collectionResponse.EnsureSuccessStatusCode();
        var collection = (await collectionResponse.Content.ReadFromJsonAsync<RecipeCollectionDetailResponse>())!;

        foreach (var title in recipeTitles) {
            var recipeResponse = await client.PostAsJsonAsync(
                $"/api/v1/workspaces/{sourceWorkspaceId}/recipes",
                new {
                    title,
                    servings = 2,
                    isArchived = false,
                    tags = Array.Empty<string>(),
                    ingredients = new[] { new { name = "Onion", amount = 1, unit = "unit", displayText = "1 onion" } },
                    steps = new[] { new { instruction = "Cook" } }
                }
            );
            recipeResponse.EnsureSuccessStatusCode();
            var recipe = (await recipeResponse.Content.ReadFromJsonAsync<RecipeResponse>())!;

            var addResponse = await client.PostAsJsonAsync(
                $"/api/v1/workspaces/{sourceWorkspaceId}/recipe-collections/{collection.Id}/recipes",
                new { recipeId = recipe.Id }
            );
            addResponse.EnsureSuccessStatusCode();
        }

        var shareResponse = await client.PostAsync(
            $"/api/v1/workspaces/{sourceWorkspaceId}/recipe-collections/{collection.Id}/share",
            EmptyJsonContent()
        );
        shareResponse.EnsureSuccessStatusCode();
        var share = (await shareResponse.Content.ReadFromJsonAsync<RecipeCollectionShareLinkResponse>())!;

        return new SharedCollection(
            client,
            userId,
            sourceWorkspaceId,
            targetWorkspace.Id,
            share.ShareToken
        );
    }

    private static StringContent EmptyJsonContent() {
        return new StringContent("{}", Encoding.UTF8, "application/json");
    }

    private sealed record SharedCollection(
        HttpClient Client,
        Guid UserId,
        Guid SourceWorkspaceId,
        Guid TargetWorkspaceId,
        string ShareToken
    );
}
