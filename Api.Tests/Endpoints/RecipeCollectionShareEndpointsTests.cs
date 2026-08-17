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
        Assert.Equal(["Sunday Roast"], preview.RecipeTitles);
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
        public string[] RecipeTitles { get; set; } = [];
    }

    private sealed class CollectionDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid OwnerWorkspaceId { get; set; }
    }
}
