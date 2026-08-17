namespace Api.Links;

/// <summary>
///     Builds links to this API's own endpoints, by route name, so a route change moves the links with it.
/// </summary>
public sealed class ResourceLinkGenerator(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
{
    public const string GetRecipeRouteName = "GetRecipe";

    /// <summary>
    ///     Absolute URL when there is a request to take the scheme and host from, otherwise a path.
    ///     Null if the named route is not registered.
    /// </summary>
    public string? Recipe(Guid workspaceId, Guid recipeId) {
        var values = new { workspaceId, recipeId };
        var httpContext = httpContextAccessor.HttpContext;
        return httpContext is null
            ? linkGenerator.GetPathByName(GetRecipeRouteName, values)
            : linkGenerator.GetUriByName(httpContext, GetRecipeRouteName, values);
    }
}
