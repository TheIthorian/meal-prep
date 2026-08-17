using Api.Endpoints.Responses.MealPrep;

namespace Api.Links;

/// <summary>
///     Attaches both link flavours to recipe payloads: the web-app page a user can open, and this API's own
///     resource URL. Handlers take this rather than both generators.
/// </summary>
public sealed class RecipeLinkService(WebAppLinkGenerator webApp, ResourceLinkGenerator resources)
{
    public WebAppLinkGenerator WebApp { get; } = webApp;

    public RecipeResponse Attach(RecipeResponse response) {
        return response.WithLinks(WebApp, resources);
    }

    public RecipeListItemResponse[] Attach(RecipeListItemResponse[] responses, Guid workspaceId) {
        return responses.Select(response => response.WithLinks(workspaceId, WebApp, resources)).ToArray();
    }
}
