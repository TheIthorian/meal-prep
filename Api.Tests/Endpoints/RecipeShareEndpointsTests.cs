using System.Net;
using System.Net.Http.Json;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Endpoints;

public sealed class RecipeShareEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public RecipeShareEndpointsTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Fact]
    public async Task PostCreateShareLink_WithoutSession_StaysProtected() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var recipeId = await SeedRecipeAsync(workspaceId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/recipes/{recipeId}/share",
            new { }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostCreateShareLink_ForOwnRecipe_ReturnsReusableSharePath() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var recipeId = await SeedRecipeAsync(workspaceId);

        using var client = factory.CreateAuthenticatedClient(userId);

        var first = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/recipes/{recipeId}/share",
            new { }
        );
        first.EnsureSuccessStatusCode();

        var link = await first.Content.ReadFromJsonAsync<ShareLinkDto>();
        Assert.NotNull(link);
        Assert.NotEmpty(link!.ShareToken);
        Assert.Equal($"/share/recipes/{link.ShareToken}", link.SharePath);

        // Sharing the same recipe again hands out the token that is already in circulation.
        var second = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/recipes/{recipeId}/share",
            new { }
        );
        second.EnsureSuccessStatusCode();

        var repeated = await second.Content.ReadFromJsonAsync<ShareLinkDto>();
        Assert.Equal(link.ShareToken, repeated!.ShareToken);

        // The reuse path reports when the link was actually created, not when it was last asked for.
        // Compared with a tolerance: the first response carries the in-memory timestamp, the second the
        // one Postgres stored, which is rounded to microseconds.
        Assert.Equal(link.CreatedAtUtc, repeated.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task PostCreateShareLink_ForRecipeInAnotherWorkspace_ReturnsNotFound() {
        var (_, ownerWorkspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var recipeId = await SeedRecipeAsync(ownerWorkspaceId);

        var (outsiderUserId, outsiderWorkspaceId) = await factory.SeedUserWithWorkspaceAsync("Outsider Workspace");
        using var client = factory.CreateAuthenticatedClient(outsiderUserId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{outsiderWorkspaceId}/recipes/{recipeId}/share",
            new { }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedRecipe_WithoutSession_ReturnsRecipeWithoutNamingTheSharingWorkspace() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken) = await SeedRecipeWithShareLinkAsync(workspaceId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-share/{shareToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<SharedRecipeDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("Sunday Roast", detail!.Title);
        Assert.Equal("A slow roasted centrepiece", detail.Description);
        Assert.Equal(4m, detail.Servings);
        Assert.Equal(["dinner"], detail.Tags);
        Assert.True(detail.HasImage);
        Assert.Equal(["Beef brisket"], detail.Ingredients.Select(ingredient => ingredient.Name));
        Assert.Equal(["Season the beef"], detail.Steps.Select(step => step.Instruction));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Sharer Workspace", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSharedRecipe_DoesNotLeakOwnerWorkspaceOrPrivateFields() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken) = await SeedRecipeWithShareLinkAsync(workspaceId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-share/{shareToken}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("workspaceId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(workspaceId.ToString(), payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("collections", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isFavorite", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSharedRecipe_WithUnknownToken_ReturnsNotFound() {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-share/{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedRecipe_WhenRecipeIsDeleted_ReturnsNotFound() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (recipeId, shareToken) = await SeedRecipeWithShareLinkAsync(workspaceId);

        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var recipe = await db.Recipes.FirstAsync(value => value.Id == recipeId);
            recipe.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-share/{shareToken}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedRecipeImage_WithoutSession_IsNotChallengedForAuthentication() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken) = await SeedRecipeWithShareLinkAsync(workspaceId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/recipe-share/{shareToken}/image");

        // The seeded object key has no bytes behind it in storage, so 404 is the expected outcome here.
        // What matters is that an anonymous caller is never challenged for a session.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSaveSharedRecipe_WithoutSession_StaysProtected() {
        var (_, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (_, shareToken) = await SeedRecipeWithShareLinkAsync(workspaceId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/recipe-share-save/{shareToken}",
            new { }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSaveSharedRecipe_CopiesTheRecipeIntoTheVisitorsWorkspace() {
        var (_, ownerWorkspaceId) = await factory.SeedUserWithWorkspaceAsync("Sharer Workspace");
        var (sourceRecipeId, shareToken) = await SeedRecipeWithShareLinkAsync(ownerWorkspaceId);

        var (visitorUserId, visitorWorkspaceId) = await factory.SeedUserWithWorkspaceAsync("Visitor Workspace");
        using var client = factory.CreateAuthenticatedClient(visitorUserId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{visitorWorkspaceId}/recipe-share-save/{shareToken}",
            new { }
        );

        response.EnsureSuccessStatusCode();

        var saved = await response.Content.ReadFromJsonAsync<SavedRecipeDto>();
        Assert.NotNull(saved);
        Assert.NotEqual(sourceRecipeId, saved!.Id);
        Assert.Equal("Sunday Roast", saved.Title);
        Assert.Equal(visitorWorkspaceId, saved.WorkspaceId);
        Assert.Equal(["Beef brisket"], saved.Ingredients.Select(ingredient => ingredient.Name));
        Assert.Equal(["Season the beef"], saved.Steps.Select(step => step.Instruction));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var storedRecipe = await db.Recipes.AsNoTracking().FirstAsync(value => value.Id == saved.Id);

        Assert.Equal(visitorWorkspaceId, storedRecipe.WorkspaceId);

        // The original is untouched: the recipient works on their own copy.
        var sourceRecipe = await db.Recipes.AsNoTracking().FirstAsync(value => value.Id == sourceRecipeId);
        Assert.Equal(ownerWorkspaceId, sourceRecipe.WorkspaceId);
    }

    [Fact]
    public async Task PostSaveSharedRecipe_WithUnknownToken_ReturnsNotFound() {
        var (visitorUserId, visitorWorkspaceId) = await factory.SeedUserWithWorkspaceAsync("Visitor Workspace");
        using var client = factory.CreateAuthenticatedClient(visitorUserId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{visitorWorkspaceId}/recipe-share-save/{Guid.NewGuid():N}",
            new { }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> SeedRecipeAsync(Guid workspaceId) {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var workspace = await db.Workspaces.FirstAsync(value => value.Id == workspaceId);
        var recipe = BuildRecipe(workspace);

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        return recipe.Id;
    }

    private async Task<(Guid RecipeId, string ShareToken)> SeedRecipeWithShareLinkAsync(Guid workspaceId) {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var workspace = await db.Workspaces.FirstAsync(value => value.Id == workspaceId);
        var creatorUserId = await db.WorkspaceUsers
            .Where(member => member.WorkspaceId == workspaceId)
            .Select(member => member.UserId)
            .FirstAsync();

        var recipe = BuildRecipe(workspace);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var token = Guid.NewGuid().ToString("N");
        db.RecipeShareLinks.Add(RecipeShareLink.CreateNew(recipe.Id, creatorUserId, token));
        await db.SaveChangesAsync();

        return (recipe.Id, token);
    }

    private static Recipe BuildRecipe(Workspace workspace) {
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

        return recipe;
    }

    private sealed class ShareLinkDto
    {
        public string ShareToken { get; set; } = string.Empty;
        public string SharePath { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class SharedRecipeDetailDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Servings { get; set; }
        public string? SourceUrl { get; set; }
        public string? Notes { get; set; }
        public string[] Tags { get; set; } = [];
        public bool HasImage { get; set; }
        public IngredientDto[] Ingredients { get; set; } = [];
        public StepDto[] Steps { get; set; } = [];
    }

    private sealed class SavedRecipeDto
    {
        public Guid Id { get; set; }
        public Guid WorkspaceId { get; set; }
        public string Title { get; set; } = string.Empty;
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
}
