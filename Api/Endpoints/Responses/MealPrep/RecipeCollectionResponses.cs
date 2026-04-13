using Api.Endpoints.Requests.MealPrep;

namespace Api.Endpoints.Responses.MealPrep;

public record RecipeCollectionListItemResponse(
    Guid Id,
    string Name,
    string? Description,
    int RecipeCount,
    Guid OwnerWorkspaceId,
    bool IsOwnedByViewerWorkspace
);

public record RecipeCollectionSharedWorkspaceResponse(Guid WorkspaceId, string WorkspaceName);

public record RecipeCollectionDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerWorkspaceId,
    bool CanEdit,
    RecipeListItemResponse[] Recipes,
    RecipeCollectionSharedWorkspaceResponse[] SharedWithWorkspaces
);

public record RecipeCollectionExportRecipe(string Title, SaveRecipeRequest Payload);

public record RecipeCollectionExportResponse(
    string CollectionName,
    string? Description,
    DateTime ExportedAtUtc,
    RecipeCollectionExportRecipe[] Recipes
);
