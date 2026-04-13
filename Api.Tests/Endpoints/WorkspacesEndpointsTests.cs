using System.Net;
using System.Net.Http.Json;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Endpoints;

public sealed class WorkspacesEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public WorkspacesEndpointsTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Fact]
    public async Task GetWorkspaces_WithoutAuthHeader_ReturnsUnauthorized() {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWorkspace_WithAuthenticatedUser_CreatesWorkspaceMembership() {
        var (userId, _) = await factory.SeedUserWithWorkspaceAsync("Seeder Workspace");
        using var client = factory.CreateAuthenticatedClient(userId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces",
            new { name = "Travel Workspace" }
        );

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WorkspaceDto>();
        Assert.NotNull(payload);
        Assert.Equal("Travel Workspace", payload!.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var membershipExists = db.WorkspaceUsers.Any(m => m.UserId == userId && m.WorkspaceId == payload.Id);

        Assert.True(membershipExists);
    }

    [Fact]
    public async Task GetWorkspaces_ReturnsOnlyCurrentUsersWorkspaces() {
        var (userA, workspaceA1) = await factory.SeedUserWithWorkspaceAsync("User A - Alpha");
        await factory.SeedUserWithWorkspaceAsync("User B - Hidden");

        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var user = db.Users.First(u => u.Id == userA);
            var secondWorkspace = Workspace.CreateNew("User A - Beta");
            db.Workspaces.Add(secondWorkspace);
            db.WorkspaceUsers.Add(WorkspaceUser.CreateNew(user, secondWorkspace, WorkspaceUser.Roles.Owner));
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateAuthenticatedClient(userA);
        var response = await client.GetAsync("/api/v1/workspaces");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<WorkspaceDto>>();
        Assert.NotNull(payload);

        Assert.Contains(payload!, w => w.Id == workspaceA1 && w.Name == "User A - Alpha");
        Assert.Contains(payload!, w => w.Name == "User A - Beta");
        Assert.DoesNotContain(payload!, w => w.Name == "User B - Hidden");
    }

    private sealed class WorkspaceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
