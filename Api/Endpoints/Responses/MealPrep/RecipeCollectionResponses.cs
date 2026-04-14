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

public record RecipeCollectionExportRecipe(Guid RecipeId, string Title, string? ImageFileName, SaveRecipeRequest Payload);

public record RecipeCollectionExportResponse(
    string CollectionName,
    string? Description,
    DateTime ExportedAtUtc,
    RecipeCollectionExportRecipe[] Recipes
);

public record RecipeCollectionShareLinkResponse(string ShareToken, string ImportPath, DateTime CreatedAtUtc);

public record RecipeCollectionShareLinkPreviewResponse(
    string CollectionName,
    string? Description,
    string OwnerWorkspaceName,
    int RecipeCount
);
