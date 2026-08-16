using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Mcp;

/// <summary>
///     Builds links into the web app for MCP responses. An assistant only ever sees ids, so
///     without these it cannot hand the user a URL for a recipe it just found or created.
/// </summary>
public sealed class McpWebLinks(IOptions<WebAppOptions> options)
{
    /// <summary>
    ///     Origin of the web app, without a trailing slash.
    /// </summary>
    public string BaseUrl { get; } = options.Value.BaseUrl;

    public string RecipeUrl(Guid workspaceId, Guid recipeId) {
        return $"{BaseUrl}/workspaces/{workspaceId}/recipe/{recipeId}";
    }

    public string RecipeLibraryUrl(Guid workspaceId) {
        return $"{BaseUrl}/workspaces/{workspaceId}/";
    }

    /// <summary>
    ///     Adds a <c>webUrl</c> property to a serialized recipe response, or to every entry of a
    ///     paginated one. Returns the input unchanged if it is not shaped like either — error
    ///     payloads flow through the same serialization path.
    /// </summary>
    public string WithRecipeLinks(string json, Guid workspaceId) {
        JsonNode? node;
        try {
            node = JsonNode.Parse(json);
        } catch (JsonException) {
            return json;
        }

        if (node is not JsonObject root)
            return json;

        if (root["data"] is JsonArray page) {
            foreach (var item in page)
                AddRecipeLink(item as JsonObject, workspaceId);
        } else {
            AddRecipeLink(root, workspaceId);
        }

        return root.ToJsonString(McpJson.SerializerOptions);
    }

    private void AddRecipeLink(JsonObject? recipe, Guid workspaceId) {
        if (recipe?["id"]?.GetValue<string>() is not { } rawId || !Guid.TryParse(rawId, out var recipeId))
            return;
        recipe["webUrl"] = RecipeUrl(workspaceId, recipeId);
    }
}
