using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Links;

/// <summary>
///     Builds links to pages in the web app. These routes belong to the SPA, so they cannot come from
///     <see cref="Microsoft.AspNetCore.Routing.LinkGenerator" /> — the origin is configured and the paths are known here.
/// </summary>
public sealed class WebAppLinkGenerator(IOptions<WebAppOptions> options)
{
    /// <summary>
    ///     Origin of the web app, without a trailing slash.
    /// </summary>
    public string BaseUrl { get; } = options.Value.BaseUrl;

    public string Recipe(Guid workspaceId, Guid recipeId) {
        return $"{BaseUrl}/workspaces/{workspaceId}/recipe/{recipeId}";
    }

    public string RecipeLibrary(Guid workspaceId) {
        return $"{BaseUrl}/workspaces/{workspaceId}/";
    }
}
