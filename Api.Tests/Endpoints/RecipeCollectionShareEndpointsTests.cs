using System.Net;
using System.Net.Http.Json;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Endpoints;

public sealed class RecipeCollectionShareEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public RecipeCollectionShareEndpointsTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Fact]
    public async Task GetShareLinkPreview_WithoutSession_ReturnsReadOnlyCollectionPreview() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken) = await SeedCollectionWithShareLinkAsync(workspaceId, "Weeknight Dinners", "Sunday Roast");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-collection-share/{shareToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.Content.ReadFromJsonAsync<SharePreviewDto>();
        Assert.NotNull(preview);
        Assert.Equal("Weeknight Dinners", preview!.CollectionName);
        Assert.Equal("Sharer Workspace", preview.OwnerWorkspaceName);
        Assert.Equal(1, preview.RecipeCount);
        Assert.Equal(["Sunday Roast"], preview.Recipes.Select(recipe => recipe.Title));
    }

    [Fact]
    public async Task GetShareLinkPreview_WithUnknownToken_ReturnsNotFoundForAnonymousVisitor() {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-collection-share/{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostImportFromShareLink_WithoutSession_StaysProtected() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken) = await SeedCollectionWithShareLinkAsync(workspaceId, "Private Plans", "Secret Stew");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/recipe-collection-import/{shareToken}",
            new { }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostImportFromShareLink_AfterVisitorSignsIn_AssociatesCollectionWithTheirWorkspace() {
        var (_, ownerWorkspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken) = await SeedCollectionWithShareLinkAsync(ownerWorkspaceId, "Weeknight Dinners", "Sunday Roast");

        var (visitorUserId, visitorWorkspaceId) = await factory.SeedUserWithWorkspaceAsync("Visitor Workspace");
        using var client = factory.CreateAuthenticatedClient(visitorUserId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{visitorWorkspaceId}/recipe-collection-import/{shareToken}",
            new { }
        );

        response.EnsureSuccessStatusCode();

        var imported = await response.Content.ReadFromJsonAsync<CollectionDetailDto>();
        Assert.NotNull(imported);
        Assert.StartsWith("Weeknight Dinners", imported!.Name);
        Assert.Equal(visitorWorkspaceId, imported.OwnerWorkspaceId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var storedCollection = await db.RecipeCollections
            .AsNoTracking()
            .FirstAsync(collection => collection.Id == imported.Id);

        Assert.Equal(visitorWorkspaceId, storedCollection.WorkspaceId);
    }

    [Fact]
    public async Task GetShareLinkPreview_WithoutSession_ReturnsRecipeSummariesWithImageFlag() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken, recipeId) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Weeknight Dinners");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-collection-share/{shareToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.Content.ReadFromJsonAsync<SharePreviewDto>();
        Assert.NotNull(preview);

        var summary = Assert.Single(preview!.Recipes);
        Assert.Equal(recipeId, summary.Id);
        Assert.Equal("Sunday Roast", summary.Title);
        Assert.Equal("A slow roasted centrepiece", summary.Description);
        Assert.Equal(25, summary.PrepMinutes);
        Assert.Equal(90, summary.CookMinutes);
        Assert.Equal(4m, summary.Servings);
        Assert.Equal(["dinner"], summary.Tags);
        Assert.True(summary.HasImage);
    }

    [Fact]
    public async Task GetSharedRecipeDetail_WithoutSession_ReturnsFullRecipe() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken, recipeId) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Weeknight Dinners");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-collection-share/{shareToken}/recipes/{recipeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<SharedRecipeDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("Sunday Roast", detail!.Title);
        Assert.Equal("A slow roasted centrepiece", detail.Description);
        Assert.Equal("Rest before carving", detail.Notes);
        Assert.Equal("https://example.test/roast", detail.SourceUrl);
        Assert.True(detail.HasImage);
        Assert.Equal(["Beef brisket"], detail.Ingredients.Select(ingredient => ingredient.Name));
        Assert.Equal(["Season the beef"], detail.Steps.Select(step => step.Instruction));
    }

    [Fact]
    public async Task GetSharedRecipeDetail_DoesNotLeakOwnerWorkspaceOrPrivateCollections() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken, recipeId) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Weeknight Dinners");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-collection-share/{shareToken}/recipes/{recipeId}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("workspaceId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(workspaceId.ToString(), payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("collections", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isFavorite", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSharedRecipeDetail_WithRecipeFromAnotherCollection_ReturnsNotFound() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, sharedToken, _) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Shared Collection");
        var (_, _, unsharedRecipeId) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Private Collection");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-collection-share/{sharedToken}/recipes/{unsharedRecipeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedRecipeDetail_WithUnknownToken_ReturnsNotFound() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, _, recipeId) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Weeknight Dinners");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            $"/api/v1/recipe-collection-share/{Guid.NewGuid():N}/recipes/{recipeId}"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedRecipeDetail_WhenRecipeIsDeleted_ReturnsNotFound() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken, recipeId) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Weeknight Dinners");

        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var recipe = await db.Recipes.FirstAsync(value => value.Id == recipeId);
            recipe.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-collection-share/{shareToken}/recipes/{recipeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedRecipeImage_WithRecipeFromAnotherCollection_ReturnsNotFound() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, sharedToken, _) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Shared Collection");
        var (_, _, unsharedRecipeId) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Private Collection");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            $"/api/v1/recipe-collection-share/{sharedToken}/recipes/{unsharedRecipeId}/image"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedRecipeImage_WithoutSession_IsNotChallengedForAuthentication() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken, recipeId) = await SeedCollectionWithDetailedRecipeAsync(workspaceId, "Weeknight Dinners");

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            $"/api/v1/recipe-collection-share/{shareToken}/recipes/{recipeId}/image"
        );

        // The seeded object key has no bytes behind it in storage, so 404 is the expected outcome here.
        // What matters is that an anonymous caller is never challenged for a session.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(Guid CollectionId, string ShareToken, Guid RecipeId)> SeedCollectionWithDetailedRecipeAsync(
        Guid workspaceId,
        string collectionName
    ) {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var workspace = await db.Workspaces.FirstAsync(value => value.Id == workspaceId);
        var creatorUserId = await db.WorkspaceUsers
            .Where(member => member.WorkspaceId == workspaceId)
            .Select(member => member.UserId)
            .FirstAsync();

        var collection = RecipeCollection.CreateNew(workspace, collectionName, "Shared with the world");
        var recipe = Recipe.CreateNew(workspace, "Sunday Roast", 4);

        recipe.UpdateDetails(
            "Sunday Roast",
            "A slow roasted centrepiece",
            4,
            "https://example.test/roast",
            "Rest before carving",
            25,
            90,
            false,
            ["dinner"]
        );
        recipe.ReplaceIngredients([
            RecipeIngredient.CreateNew(0, "Beef brisket", "1.5kg beef brisket", 1.5m, "kg", "beef brisket", null, null),
        ]);
        recipe.ReplaceSteps([RecipeStep.CreateNew(0, "Season the beef", null)]);
        recipe.SetImageObjectKey($"recipes/{Guid.NewGuid():N}/photo.jpg");

        db.RecipeCollections.Add(collection);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        db.RecipeCollectionRecipes.Add(RecipeCollectionRecipe.CreateNew(collection.Id, recipe.Id, 0));

        var token = Guid.NewGuid().ToString("N");
        db.RecipeCollectionShareLinks.Add(RecipeCollectionShareLink.CreateNew(collection.Id, creatorUserId, token));
        await db.SaveChangesAsync();

        return (collection.Id, token, recipe.Id);
    }

    private async Task<(Guid CollectionId, string ShareToken)> SeedCollectionWithShareLinkAsync(
        Guid workspaceId,
        string collectionName,
        string recipeTitle
    ) {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var workspace = await db.Workspaces.FirstAsync(value => value.Id == workspaceId);
        var creatorUserId = await db.WorkspaceUsers
            .Where(member => member.WorkspaceId == workspaceId)
            .Select(member => member.UserId)
            .FirstAsync();

        var collection = RecipeCollection.CreateNew(workspace, collectionName, "Shared with the world");
        var recipe = Recipe.CreateNew(workspace, recipeTitle, 4);

        db.RecipeCollections.Add(collection);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        db.RecipeCollectionRecipes.Add(RecipeCollectionRecipe.CreateNew(collection.Id, recipe.Id, 0));

        var token = Guid.NewGuid().ToString("N");
        db.RecipeCollectionShareLinks.Add(RecipeCollectionShareLink.CreateNew(collection.Id, creatorUserId, token));
        await db.SaveChangesAsync();

        return (collection.Id, token);
    }

    private sealed class SharePreviewDto
    {
        public string CollectionName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OwnerWorkspaceName { get; set; } = string.Empty;
        public int RecipeCount { get; set; }
        public SharedRecipeSummaryDto[] Recipes { get; set; } = [];
    }

    private sealed class SharedRecipeSummaryDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Servings { get; set; }
        public int? PrepMinutes { get; set; }
        public int? CookMinutes { get; set; }
        public string[] Tags { get; set; } = [];
        public bool HasImage { get; set; }
    }

    private sealed class SharedRecipeDetailDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Servings { get; set; }
        public string? SourceUrl { get; set; }
        public string? Notes { get; set; }
        public int? PrepMinutes { get; set; }
        public int? CookMinutes { get; set; }
        public string[] Tags { get; set; } = [];
        public bool HasImage { get; set; }
        public IngredientDto[] Ingredients { get; set; } = [];
        public StepDto[] Steps { get; set; } = [];
    }

    private sealed class IngredientDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
    }

    private sealed class StepDto
    {
        public int SortOrder { get; set; }
        public string Instruction { get; set; } = string.Empty;
    }

    private sealed class CollectionDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid OwnerWorkspaceId { get; set; }
    }
}
