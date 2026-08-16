using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests.Endpoints;

public sealed class CompressionTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public CompressionTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task GetWorkspaces_WithAcceptEncoding_ReturnsCompressedBody(string encoding) {
        var (userId, _) = await factory.SeedUserWithWorkspaceAsync("Compression Workspace");
        using var client = factory.CreateAuthenticatedClient(userId);
        client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));

        var response = await client.GetAsync("/api/v1/workspaces");

        response.EnsureSuccessStatusCode();
        Assert.Equal(encoding, Assert.Single(response.Content.Headers.ContentEncoding));
    }

    [Fact]
    public async Task GetWorkspaces_WithoutAcceptEncoding_ReturnsUncompressedBody() {
        var (userId, _) = await factory.SeedUserWithWorkspaceAsync("Uncompressed Workspace");
        using var client = factory.CreateAuthenticatedClient(userId);

        var response = await client.GetAsync("/api/v1/workspaces");

        response.EnsureSuccessStatusCode();
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task AuthEndpoint_WithAcceptEncoding_IsNotCompressed() {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = $"missing-{Guid.NewGuid():N}@tests.local", password = "not-a-password" }
        );

        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task PostWorkspace_WithGzippedBody_IsDecompressed() {
        var (userId, _) = await factory.SeedUserWithWorkspaceAsync("Decompression Seed");
        using var client = factory.CreateAuthenticatedClient(userId);

        using var body = new ByteArrayContent(Gzip("""{"name":"Gzipped Workspace"}"""));
        body.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        body.Headers.ContentEncoding.Add("gzip");

        var response = await client.PostAsync("/api/v1/workspaces", body);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceNameDto>();
        Assert.Equal("Gzipped Workspace", payload!.Name);
    }

    private static byte[] Gzip(string json) {
        using var target = new MemoryStream();
        using (var gzip = new GZipStream(target, CompressionLevel.Fastest, leaveOpen: true)) {
            gzip.Write(System.Text.Encoding.UTF8.GetBytes(json));
        }

        return target.ToArray();
    }

    private sealed class WorkspaceNameDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
